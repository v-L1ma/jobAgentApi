using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace jobAgentApi.Infrastructure.Services.JobScraper;

/// <summary>
/// Gerencia uma única instância compartilhada do navegador Playwright para evitar problemas de performance.
/// Reaproveita contextos de browser para múltiplos scrapers.
/// </summary>
internal interface IPlaywrightBrowserManager : IAsyncDisposable
{
    Task<IBrowserContext> GetOrCreateContextAsync(string contextName, CancellationToken cancellationToken);
    Task ReleaseBrowserAsync();
}

internal sealed class PlaywrightBrowserManager : IPlaywrightBrowserManager
{
    private const int MaxContexts = 3;
    private static readonly TimeSpan ContextIdleTimeout = TimeSpan.FromMinutes(20);

    private readonly JobScraperOptions _options;
    private readonly ILogger<PlaywrightBrowserManager> _logger;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private readonly Dictionary<string, ContextEntry> _contexts = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public PlaywrightBrowserManager(IOptions<JobScraperOptions> options, ILogger<PlaywrightBrowserManager> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IBrowserContext> GetOrCreateContextAsync(string contextName, CancellationToken cancellationToken)
    {
        await CleanupStaleContextsAsync();

        IBrowserContext? existingContext = null;

        lock (_lock)
        {
            if (_contexts.TryGetValue(contextName, out var cachedContext))
            {
                cachedContext.LastUsedUtc = DateTime.UtcNow;
                existingContext = cachedContext.Context;
            }
        }

        if (existingContext is not null)
        {
            try
            {
                _ = existingContext.Pages.Count;
                _logger.LogDebug("Reusing existing browser context: {ContextName}", contextName);
                return existingContext;
            }
            catch (PlaywrightException ex)
            {
                _logger.LogWarning(ex, "Discarding closed/invalid browser context: {ContextName}", contextName);

                lock (_lock)
                {
                    if (_contexts.TryGetValue(contextName, out var trackedContext) && ReferenceEquals(trackedContext.Context, existingContext))
                    {
                        _contexts.Remove(contextName);
                    }
                }
            }
        }

        // Initialize browser if not already done
        if (_browser is null || !_browser.IsConnected)
        {
            await ReleaseBrowserAsync();
            await InitializeBrowserAsync(cancellationToken);
        }

        await EnforceContextLimitAsync();

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1366, Height = 900 }
        });

        lock (_lock)
        {
            _contexts[contextName] = new ContextEntry(context, DateTime.UtcNow);
        }

        _logger.LogInformation("Created new browser context: {ContextName}", contextName);
        return context;
    }

    public async Task ReleaseBrowserAsync()
    {
        var contextsToClose = new List<IBrowserContext>();

        lock (_lock)
        {
            foreach (var entry in _contexts.Values)
            {
                contextsToClose.Add(entry.Context);
            }

            _contexts.Clear();
        }

        foreach (var context in contextsToClose)
        {
            try
            {
                await context.CloseAsync();
            }
            catch (PlaywrightException ex)
            {
                _logger.LogDebug(ex, "Ignoring browser context close failure during release");
            }
        }

        if (_browser is not null)
        {
            await _browser.CloseAsync();
            _browser = null;
        }

        _playwright?.Dispose();
        _playwright = null;

        _logger.LogInformation("Browser released");
    }

    private async Task InitializeBrowserAsync(CancellationToken cancellationToken)
    {
        if (_playwright is not null)
        {
            return;
        }

        _playwright = await Playwright.CreateAsync();

        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = _options.Headless,
            SlowMo = _options.SlowMoMs,
            Args = new[]
            {
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-dev-shm-usage",
                "--disable-gpu",
                "--disable-extensions"
            }
        });

        _logger.LogInformation("Browser initialized successfully");
    }

    private async Task CleanupStaleContextsAsync()
    {
        var now = DateTime.UtcNow;
        var contextsToClose = new List<IBrowserContext>();
        var staleContextCount = 0;

        lock (_lock)
        {
            var keysToRemove = new List<string>();

            foreach (var pair in _contexts)
            {
                if (now - pair.Value.LastUsedUtc > ContextIdleTimeout)
                {
                    keysToRemove.Add(pair.Key);
                    contextsToClose.Add(pair.Value.Context);
                }
            }

            staleContextCount = keysToRemove.Count;

            foreach (var key in keysToRemove)
            {
                _contexts.Remove(key);
            }
        }

        foreach (var context in contextsToClose)
        {
            try
            {
                await context.CloseAsync();
            }
            catch (PlaywrightException ex)
            {
                _logger.LogDebug(ex, "Ignoring stale browser context close failure");
            }
        }

        if (staleContextCount > 0)
        {
            _logger.LogDebug("Cleaned {StaleContextCount} stale browser contexts", staleContextCount);
        }
    }

    private async Task EnforceContextLimitAsync()
    {
        var contextsToClose = new List<IBrowserContext>();

        lock (_lock)
        {
            while (_contexts.Count >= MaxContexts)
            {
                string? oldestKey = null;
                DateTime oldestUsage = DateTime.MaxValue;

                foreach (var pair in _contexts)
                {
                    if (pair.Value.LastUsedUtc < oldestUsage)
                    {
                        oldestUsage = pair.Value.LastUsedUtc;
                        oldestKey = pair.Key;
                    }
                }

                if (oldestKey is null)
                {
                    break;
                }

                contextsToClose.Add(_contexts[oldestKey].Context);
                _contexts.Remove(oldestKey);
            }
        }

        foreach (var context in contextsToClose)
        {
            try
            {
                await context.CloseAsync();
            }
            catch (PlaywrightException ex)
            {
                _logger.LogDebug(ex, "Ignoring browser context close failure while enforcing limit");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await ReleaseBrowserAsync();
    }

    private sealed class ContextEntry
    {
        public ContextEntry(IBrowserContext context, DateTime lastUsedUtc)
        {
            Context = context;
            LastUsedUtc = lastUsedUtc;
        }

        public IBrowserContext Context { get; }
        public DateTime LastUsedUtc { get; set; }
    }
}
