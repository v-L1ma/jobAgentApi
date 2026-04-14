using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace jobAgentApi.Infrastructure.Services.JobScraper;

internal interface IVagasComBrJobScraper
{
    Task StreamJobsAsync(
        string query,
        string location,
        Func<VagasComBrScrapedJob, Task<bool>> onJob,
        CancellationToken cancellationToken);
}

internal sealed record VagasComBrScrapedJob(
    string Id,
    string Title,
    string Company,
    string Url,
    string Location,
    string? Description);

internal sealed class VagasComBrJobScraper : IVagasComBrJobScraper
{
    private const string BaseSearchUrl = "https://www.vagas.com.br/vagas-de-";
    
    private readonly JobScraperOptions _options;
    private readonly ILogger<VagasComBrJobScraper> _logger;
    private readonly IPlaywrightBrowserManager _browserManager;

    public VagasComBrJobScraper(
        IOptions<JobScraperOptions> options,
        ILogger<VagasComBrJobScraper> logger,
        IPlaywrightBrowserManager browserManager)
    {
        _options = options.Value;
        _logger = logger;
        _browserManager = browserManager;
    }

    public async Task StreamJobsAsync(
        string query,
        string location,
        Func<VagasComBrScrapedJob, Task<bool>> onJob,
        CancellationToken cancellationToken)
    {
        var keywords = ParseKeywordsFromQuery(query);
        var desiredLocations = ParseDesiredLocations(location);

        if (keywords.Count == 0)
        {
            _logger.LogWarning("Query vazia ou inválida para Vagas.com.br");
            return;
        }

        _logger.LogInformation(
            "Iniciando scraping no Vagas.com.br para {KeywordCount} keywords: {Keywords}",
            keywords.Count,
            string.Join(", ", keywords));

        var maxParallelKeywords = Math.Clamp(
            _options.MaxVagasComBrParallelKeywords,
            1,
            Math.Max(1, keywords.Count));

        _logger.LogWarning("Vagas.com.br keyword parallelism set to {Parallelism}", maxParallelKeywords);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var processedJobIds = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        try
        {
            await Parallel.ForEachAsync(
                keywords,
                new ParallelOptions
                {
                    CancellationToken = linkedCts.Token,
                    MaxDegreeOfParallelism = maxParallelKeywords
                },
                async (keyword, ct) =>
                {
                    try
                    {
                        await ScrapeJobsAsync(
                            keyword,
                            desiredLocations,
                            processedJobIds,
                            async job =>
                            {
                                var shouldContinue = await onJob(job);
                                if (!shouldContinue)
                                {
                                    linkedCts.Cancel();
                                }

                                return shouldContinue;
                            },
                            ct);
                    }
                    catch (OperationCanceledException) when (linkedCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                    {
                        // Cancelamento esperado quando callback pede interrupcao.
                    }
                    catch (TimeoutException ex)
                    {
                        _logger.LogWarning(ex, "Timeout no scraping Vagas.com.br para keyword {Keyword}. Seguindo para as demais.", keyword);
                    }
                    catch (PlaywrightException ex)
                    {
                        _logger.LogWarning(ex, "Falha Playwright no scraping Vagas.com.br para keyword {Keyword}. Seguindo para as demais.", keyword);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Erro inesperado no scraping Vagas.com.br para keyword {Keyword}. Seguindo para as demais.", keyword);
                    }
                });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Scraping do Vagas.com.br interrompido por sinal de parada do callback");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante scraping do Vagas.com.br para query {Query}", query);
            throw;
        }
    }

    private async Task ScrapeJobsAsync(
        string keyword,
        List<string> desiredLocations,
        ConcurrentDictionary<string, byte> sharedProcessedJobIds,
        Func<VagasComBrScrapedJob, Task<bool>> onJob,
        CancellationToken cancellationToken)
    {
        var context = await _browserManager.GetOrCreateContextAsync("vagas-com-br", cancellationToken);
        IPage? page = null;

        var searchUrl = $"{BaseSearchUrl}{keyword}";
        _logger.LogInformation("Acessando URL: {Url}", searchUrl);

        try
        {
            page = await context.NewPageAsync();

            await page.RouteAsync("**/*", async route =>
            {
                var resourceType = route.Request.ResourceType;
                if (resourceType is "image" or "media" or "font")
                {
                    await route.AbortAsync();
                    return;
                }

                await route.ContinueAsync();
            });

            page.SetDefaultTimeout(Math.Max(_options.NavigationTimeoutMs, 15000));
            page.SetDefaultNavigationTimeout(Math.Max(_options.NavigationTimeoutMs * 2, 30000));

            var couldNavigate = await TryNavigateWithFallbackAsync(page, searchUrl, cancellationToken);
            if (!couldNavigate)
            {
                _logger.LogWarning("Nao foi possivel carregar a URL {Url}. Keyword sera ignorada nesta execucao.", searchUrl);
                return;
            }

            await RandomDelayAsync(cancellationToken);

            var jobsFound = 0;
            var maxJobs = _options.MaxJobsPerQuery;
            var keywordProcessedJobIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (jobsFound < maxJobs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var jobElements = await page.QuerySelectorAllAsync("article.vaga, li.vaga-item, .vaga, [class*='vaga-item']");

                if (jobElements.Count == 0)
                {
                    _logger.LogDebug("Nenhum elemento de vaga encontrado na página atual");
                    break;
                }

                foreach (var jobElement in jobElements)
                {
                    if (jobsFound >= maxJobs)
                    {
                        return;
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var job = await ExtractJobDataAsync(jobElement);
                        if (job == null)
                        {
                            _logger.LogWarning("[VagasComBr] Job SKIPPED reason=invalid_or_incomplete_data keyword={Keyword}", keyword);
                            continue;
                        }

                        if (!keywordProcessedJobIds.Add(job.Id))
                        {
                            _logger.LogWarning("[VagasComBr] Job SKIPPED reason=already_processed_in_keyword jobId={JobId}", job.Id);
                            continue;
                        }

                        if (!sharedProcessedJobIds.TryAdd(job.Id, 0))
                        {
                            _logger.LogWarning("[VagasComBr] Job SKIPPED reason=already_processed_in_session jobId={JobId}", job.Id);
                            continue;
                        }

                        if (desiredLocations.Count > 0 && !MatchesLocation(job.Location, desiredLocations))
                        {
                            _logger.LogWarning(
                                "[VagasComBr] Job SKIPPED reason=location_mismatch jobId={JobId} location={Location}",
                                job.Id,
                                job.Location);
                            continue;
                        }

                        _logger.LogWarning(
                            "[VagasComBr] Job FOUND jobId={JobId} title={Title} company={Company}",
                            job.Id,
                            job.Title,
                            job.Company);

                        var shouldContinue = await onJob(job);
                        if (!shouldContinue)
                        {
                            _logger.LogWarning("[VagasComBr] Job SKIPPED reason=callback_requested_stop jobId={JobId}", job.Id);
                            return;
                        }

                        jobsFound++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Erro ao processar elemento de vaga");
                        continue;
                    }
                }

                var hasMore = await TryLoadMoreAsync(page, cancellationToken);
                if (!hasMore)
                {
                    break;
                }

                await RandomDelayAsync(cancellationToken);
            }
        }
        finally
        {
            if (page is not null)
            {
                try
                {
                    await page.CloseAsync();
                }
                catch (PlaywrightException ex)
                {
                    _logger.LogDebug(ex, "Falha ao fechar pagina no Vagas.com.br");
                }
            }
        }
    }

    private async Task<bool> TryNavigateWithFallbackAsync(
        IPage page,
        string searchUrl,
        CancellationToken cancellationToken)
    {
        var primaryTimeout = Math.Max(_options.NavigationTimeoutMs * 2, 30000);
        var fallbackTimeout = Math.Max(primaryTimeout + 15000, 45000);

        try
        {
            await page.GotoAsync(searchUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = primaryTimeout
            });

            return true;
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex,
                "Timeout ao carregar {Url} com DOMContentLoaded ({TimeoutMs}ms). Tentando fallback COMMIT.",
                searchUrl,
                primaryTimeout);
        }

        try
        {
            await page.GotoAsync(searchUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.Commit,
                Timeout = fallbackTimeout
            });

            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or PlaywrightException)
        {
            _logger.LogWarning(ex,
                "Falha no fallback de navegacao para {Url} com COMMIT ({TimeoutMs}ms).",
                searchUrl,
                fallbackTimeout);
            return false;
        }
    }

    private async Task<VagasComBrScrapedJob?> ExtractJobDataAsync(IElementHandle jobElement)
    {
        try
        {
            var cardData = await jobElement.EvaluateAsync<VagasCardData>("""
            node => {
                const readText = (selectors) => {
                    for (const selector of selectors) {
                        const element = node.querySelector(selector);
                        if (element && element.textContent) {
                            const value = element.textContent.trim();
                            if (value.length > 0) return value;
                        }
                    }
                    return '';
                };

                const readHref = (selectors) => {
                    for (const selector of selectors) {
                        const element = node.querySelector(selector);
                        if (element) {
                            const value = element.getAttribute('href');
                            if (value && value.trim().length > 0) return value.trim();
                        }
                    }
                    return '';
                };

                return {
                    Title: readText(['h2 a', '.titulo-vaga a', '.nome-vaga a', 'a[href*="/vagas-de-"]', 'h2', '.titulo', '.titulo-vaga', '.nome-vaga']),
                    Href: readHref(['h2 a', '.titulo-vaga a', '.nome-vaga a', 'a[href*="/vagas-de-"]', 'a[href]']),
                    Company: readText(['.empresa', '.nome-empresa', '[class*="empresa"]']) || 'Empresa não informada',
                    Location: readText(['.localizacao', '.local', '[class*="localizacao"]', '[class*="local"]']) || 'Local não informado',
                    Description: readText(['.descricao', '.descricao-vaga', '.resumo-vaga', 'p'])
                };
            }
            """) ?? new VagasCardData();

            var title = CleanText(cardData.Title);
            if (string.IsNullOrWhiteSpace(title) || title == "Título não encontrado")
            {
                return null;
            }

            var href = cardData.Href;
            var url = NormalizeUrl(href);
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            var jobId = ExtractJobIdFromUrl(url);
            if (string.IsNullOrWhiteSpace(jobId))
            {
                jobId = GenerateJobId(url, title);
            }

            var company = CleanText(cardData.Company);
            var location = CleanText(cardData.Location);
            var description = CleanText(cardData.Description);

            return new VagasComBrScrapedJob(
                jobId,
                title,
                company,
                url,
                location,
                description);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao extrair dados da vaga");
            return null;
        }
    }

    private async Task<bool> TryLoadMoreAsync(IPage page, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var loadMoreButton = await page.QuerySelectorAsync(
                "button:has-text('Carregar mais'), a:has-text('Carregar mais'), [class*='carregar-mais'], button[class*='load-more']");

            if (loadMoreButton != null)
            {
                await loadMoreButton.ClickAsync();
                await page.WaitForTimeoutAsync(2000);
                return true;
            }

            var initialScrollCount = _options.MaxScrollIterations > 0 ? _options.MaxScrollIterations : 5;
            for (var i = 0; i < initialScrollCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)");
                await page.WaitForTimeoutAsync(1500);

                var newContent = await page.QuerySelectorAsync(
                    "article.vaga:nth-of-type(n+20), li.vaga-item:nth-of-type(n+20)");

                if (newContent == null)
                {
                    break;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Erro ao tentar carregar mais vagas");
            return false;
        }
    }

    private static List<string> ParseKeywordsFromQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var keywordsExpression = query.Trim();

        var andIndex = keywordsExpression.IndexOf(" AND ", StringComparison.OrdinalIgnoreCase);
        if (andIndex >= 0)
        {
            keywordsExpression = keywordsExpression[..andIndex];
        }

        keywordsExpression = keywordsExpression
            .Replace("(", " ")
            .Replace(")", " ")
            .Trim();

        if (string.IsNullOrWhiteSpace(keywordsExpression))
        {
            return [];
        }

        return Regex.Split(keywordsExpression, @"\s+OR\s+", RegexOptions.IgnoreCase)
            .Select(CleanText)
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Select(NormalizeKeywordForUrl)
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeKeywordForUrl(string keyword)
    {
        var terms = keyword
            .Trim()
            .ToLowerInvariant()
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return string.Join("-", terms);
    }

    private static string NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        if (url.StartsWith("http"))
        {
            return url.Trim();
        }

        if (url.StartsWith("/"))
        {
            return $"https://www.vagas.com.br{url.Trim()}";
        }

        return $"https://www.vagas.com.br{url.Trim()}";
    }

    private static string ExtractJobIdFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var segments = uri.Segments;
            var lastSegment = segments.LastOrDefault()?.TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(lastSegment) && Guid.TryParse(lastSegment, out var guid))
            {
                return guid.ToString();
            }

            if (!string.IsNullOrWhiteSpace(lastSegment) && long.TryParse(lastSegment, out var id))
            {
                return id.ToString();
            }
        }
        catch
        {
            // Ignora erros de parsing de URL
        }

        return string.Empty;
    }

    private static string GenerateJobId(string url, string title)
    {
        var combined = $"{url}|{title}";
        var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string CleanText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return text
            .Replace("\n", " ")
            .Replace("\r", " ")
            .Replace("\t", " ")
            .Replace("  ", " ")
            .Trim();
    }

    private static bool MatchesLocation(string jobLocation, List<string> desiredLocations)
    {
        var normalizedLocation = NormalizeLocationText(jobLocation);
        return desiredLocations.Any(loc => normalizedLocation.Contains(loc));
    }

    private static string NormalizeLocationText(string value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static List<string> ParseDesiredLocations(string locationConfig)
    {
        return (locationConfig ?? string.Empty)
            .Split([';', ',', '|'], StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeLocationText)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    private async Task RandomDelayAsync(CancellationToken cancellationToken)
    {
        if (_options.MaxDelayMs <= 0)
        {
            return;
        }

        var min = Math.Max(0, _options.MinDelayMs);
        var max = Math.Max(min, _options.MaxDelayMs);
        var delay = Random.Shared.Next(min, max + 1);
        await Task.Delay(delay, cancellationToken);
    }

    private sealed class VagasCardData
    {
        public string Title { get; init; } = string.Empty;
        public string Href { get; init; } = string.Empty;
        public string Company { get; init; } = string.Empty;
        public string Location { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
    }
}
