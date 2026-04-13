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
    private readonly JobScraperOptions _options;
    private readonly ILogger<PlaywrightBrowserManager> _logger;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private readonly Dictionary<string, IBrowserContext> _contexts = new();
    private readonly object _lock = new();

    public PlaywrightBrowserManager(IOptions<JobScraperOptions> options, ILogger<PlaywrightBrowserManager> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IBrowserContext> GetOrCreateContextAsync(string contextName, CancellationToken cancellationToken)
    {
        IBrowserContext? existingContext = null;

        lock (_lock)
        {
            if (_contexts.TryGetValue(contextName, out var cachedContext))
            {
                existingContext = cachedContext;
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
                    if (_contexts.TryGetValue(contextName, out var trackedContext) && ReferenceEquals(trackedContext, existingContext))
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

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1366, Height = 900 }
        });

        lock (_lock)
        {
            _contexts[contextName] = context;
        }

        _logger.LogInformation("Created new browser context: {ContextName}", contextName);
        return context;
    }

    public async Task ReleaseBrowserAsync()
    {
        lock (_lock)
        {
            _contexts.Clear();
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
                "--disable-setuid-sandbox"
            }
        });

        _logger.LogInformation("Browser initialized successfully");
    }

    public async ValueTask DisposeAsync()
    {
        await ReleaseBrowserAsync();
    }
}
