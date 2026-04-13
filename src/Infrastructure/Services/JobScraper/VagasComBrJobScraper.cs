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

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var processedJobIds = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var tasks = keywords
                .Select(keyword => ScrapeJobsAsync(
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
                    linkedCts.Token))
                .ToList();

            await Task.WhenAll(tasks);
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

            await page.GotoAsync(searchUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = _options.NavigationTimeoutMs * 2
            });

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
                            continue;
                        }

                        if (!keywordProcessedJobIds.Add(job.Id))
                        {
                            continue;
                        }

                        if (!sharedProcessedJobIds.TryAdd(job.Id, 0))
                        {
                            continue;
                        }

                        if (desiredLocations.Count > 0 && !MatchesLocation(job.Location, desiredLocations))
                        {
                            continue;
                        }

                        var shouldContinue = await onJob(job);
                        if (!shouldContinue)
                        {
                            _logger.LogInformation("Callback solicitou parada do scraping");
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
                await page.CloseAsync();
            }
        }
    }

    private async Task<VagasComBrScrapedJob?> ExtractJobDataAsync(IElementHandle jobElement)
    {
        try
        {
            var titleElement = await jobElement.QuerySelectorAsync("h2 a, .titulo-vaga a, .nome-vaga a, a[href*='/vagas-de-']");
            var title = titleElement != null
                ? await titleElement.InnerTextAsync()
                : await jobElement.QuerySelectorAsync("h2, .titulo, .titulo-vaga, .nome-vaga")
                    .ContinueWith(t => t.Result?.InnerTextAsync().Result ?? "Título não encontrado");

            title = CleanText(title);
            if (string.IsNullOrWhiteSpace(title) || title == "Título não encontrado")
            {
                return null;
            }

            var href = titleElement != null
                ? await titleElement.GetAttributeAsync("href")
                : null;

            if (string.IsNullOrWhiteSpace(href))
            {
                var linkElement = await jobElement.QuerySelectorAsync("a[href]");
                href = linkElement != null ? await linkElement.GetAttributeAsync("href") : string.Empty;
            }

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

            var companyElement = await jobElement.QuerySelectorAsync(".empresa, .nome-empresa, [class*='empresa']");
            var company = companyElement != null
                ? await companyElement.InnerTextAsync()
                : "Empresa não informada";

            company = CleanText(company);

            var locationElement = await jobElement.QuerySelectorAsync(".localizacao, .local, [class*='localizacao'], [class*='local']");
            var location = locationElement != null
                ? await locationElement.InnerTextAsync()
                : "Local não informado";

            location = CleanText(location);

            var descriptionElement = await jobElement.QuerySelectorAsync(".descricao, .descricao-vaga, .resumo-vaga, p");
            var description = descriptionElement != null
                ? await descriptionElement.InnerTextAsync()
                : null;

            description = CleanText(description);

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
}
