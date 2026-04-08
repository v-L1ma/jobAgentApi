using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace jobAgentApi.Infrastructure.Services.JobScraper;

internal interface ILinkedInJobScraper
{
    Task StreamJobsAsync(
        string query,
        string location,
    string? liAtCookie,
        bool easyApplyOnly,
        Func<LinkedInScrapedJob, Task<bool>> onJob,
        CancellationToken cancellationToken);
}

internal sealed record LinkedInScrapedJob(
    string Id,
    string Title,
    string Company,
    string Url,
    string Location,
    bool EasyApply,
    string? Description);

internal sealed class LinkedInJobScraper : ILinkedInJobScraper
{
    private const string JobCardSelector = "li[data-occludable-job-id]";
    private const string JobListContainerSelector = ".scaffold-layout__list";
    private const string JobDetailsReadySelector = "h1, h2, button:has-text('Easy Apply'), button:has-text('Candidatura simplificada'), [data-test-modal-id=\"easy-apply-modal\"]";
    private const string DetailsContainerSelector = ".job-details-jobs-unified-top-card__primary-description-container";
    private const string DetailsDescriptionSelector = ".jobs-description, #job-details, .jobs-description__content";

    private readonly JobScraperOptions _options;
    private readonly ILogger<LinkedInJobScraper> _logger;

    public LinkedInJobScraper(IOptions<JobScraperOptions> options, ILogger<LinkedInJobScraper> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task StreamJobsAsync(
        string query,
        string location,
        string? liAtCookie,
        bool easyApplyOnly,
        Func<LinkedInScrapedJob, Task<bool>> onJob,
        CancellationToken cancellationToken)
    {
        var desiredLocations = ParseDesiredLocations(location);
        var searchUrl = BuildSearchUrl(query, easyApplyOnly);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = _options.Headless,
            SlowMo = _options.SlowMoMs,
            Args = new[]
            {
                "--no-sandbox",
                "--disable-setuid-sandbox"
            }
        });

        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1366, Height = 900 }
        });

        if (!string.IsNullOrWhiteSpace(liAtCookie))
        {
            await context.AddCookiesAsync([
                new Cookie
                {
                    Name = "li_at",
                    Value = liAtCookie,
                    Domain = ".linkedin.com",
                    Path = "/",
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteAttribute.Lax
                }
            ]);

            _logger.LogInformation("LinkedIn li_at cookie injected for authenticated scraping session");
        }

        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(_options.NavigationTimeoutMs);

        _logger.LogInformation("Navigating LinkedIn search page {SearchUrl}", searchUrl);
        await page.GotoAsync(searchUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = _options.NavigationTimeoutMs
        });

        var processedJobIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var iteration = 0; iteration < _options.MaxScrollIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await page.WaitForSelectorAsync(JobListContainerSelector, new PageWaitForSelectorOptions
            {
                Timeout = _options.NavigationTimeoutMs
            }).ConfigureAwait(false);

            var cards = await page.QuerySelectorAllAsync(JobCardSelector);
            var newJobsFound = 0;

            for (var i = 0; i < cards.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                cards = await page.QuerySelectorAllAsync(JobCardSelector);
                if (i >= cards.Count)
                {
                    break;
                }

                var card = cards[i];
                var jobId = await card.GetAttributeAsync("data-occludable-job-id");
                if (string.IsNullOrWhiteSpace(jobId) || processedJobIds.Contains(jobId))
                {
                    continue;
                }

                processedJobIds.Add(jobId);
                newJobsFound++;

                try
                {
                    await card.ScrollIntoViewIfNeededAsync();
                    await page.WaitForTimeoutAsync(500);
                    await card.ClickAsync();
                }
                catch (PlaywrightException)
                {
                    continue;
                }

                await page.WaitForSelectorAsync(JobDetailsReadySelector, new PageWaitForSelectorOptions
                {
                    Timeout = _options.NavigationTimeoutMs
                }).ConfigureAwait(false);

                await RandomDelayAsync(cancellationToken);

                var (isValidLocation, description) = await ValidateJobDetailsAsync(page, desiredLocations);
                if (!isValidLocation)
                {
                    continue;
                }

                var scraped = await ExtractCardDataAsync(card, jobId);
                var job = new LinkedInScrapedJob(
                    jobId,
                    string.IsNullOrWhiteSpace(scraped.Title) ? "Unknown" : scraped.Title,
                    string.IsNullOrWhiteSpace(scraped.Company) ? "Unknown" : scraped.Company,
                    string.IsNullOrWhiteSpace(scraped.Url) ? $"https://www.linkedin.com/jobs/view/{jobId}/" : scraped.Url,
                    string.IsNullOrWhiteSpace(scraped.Location) ? "Unknown" : scraped.Location,
                    scraped.EasyApply,
                    description);

                var shouldContinue = await onJob(job);
                if (!shouldContinue)
                {
                    return;
                }

                await RandomDelayAsync(cancellationToken);
            }

            if (newJobsFound == 0)
            {
                await page.EvaluateAsync(
                    "selector => { const container = document.querySelector(selector); if (container) container.scrollBy(0, 1000); }",
                    JobListContainerSelector);

                await page.WaitForTimeoutAsync(2000);

                var items = await page.QuerySelectorAllAsync(JobCardSelector);
                var foundNew = false;
                foreach (var item in items)
                {
                    var id = await item.GetAttributeAsync("data-occludable-job-id");
                    if (!string.IsNullOrWhiteSpace(id) && !processedJobIds.Contains(id))
                    {
                        foundNew = true;
                        break;
                    }
                }

                if (!foundNew)
                {
                    break;
                }
            }
            else
            {
                await page.EvaluateAsync(
                    "selector => { const el = document.querySelector(selector); if (el) el.scrollBy({ top: Math.floor(el.clientHeight * 0.85), behavior: 'smooth' }); }",
                    JobListContainerSelector);

                await page.WaitForTimeoutAsync(2000);
            }
        }
    }

    private static string BuildSearchUrl(string query, bool easyApplyOnly)
    {
        var keyword = Uri.EscapeDataString(query);
        var easyApply = easyApplyOnly ? "&f_AL=true" : string.Empty;
        return $"https://www.linkedin.com/jobs/search/?keywords={keyword}{easyApply}";
    }

    private static List<string> ParseDesiredLocations(string locationConfig)
    {
        return (locationConfig ?? string.Empty)
            .Split([';', ',', '|'], StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeText)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    private static string NormalizeText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private async Task<(bool IsValid, string? Description)> ValidateJobDetailsAsync(IPage page, IReadOnlyCollection<string> desiredLocations)
    {
        try
        {
            await page.WaitForSelectorAsync(DetailsContainerSelector, new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = Math.Min(_options.NavigationTimeoutMs, 5000)
            });

            var rawText = await page.Locator(DetailsContainerSelector).InnerTextAsync();
            var normalizedText = NormalizeText(rawText);
            var hasLocation = desiredLocations.Count == 0 || desiredLocations.Any(location => normalizedText.Contains(location));

            string? description;
            try
            {
                description = await page.Locator(DetailsDescriptionSelector).First.InnerTextAsync();
            }
            catch
            {
                description = rawText;
            }

            return (hasLocation, description);
        }
        catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
        {
            _logger.LogWarning(ex, "Error validating job details in LinkedIn");
            return (false, null);
        }
    }

    private static async Task<LinkedInCardData> ExtractCardDataAsync(IElementHandle card, string jobId)
    {
        return await card.EvaluateAsync<LinkedInCardData>(@"(node, id) => {
            const root = node;
            const anchor = root.querySelector('a[href]');
            const titleFromStrong = root.querySelector('strong')?.textContent?.trim();
            const titleFromHeading = root.querySelector('h3')?.textContent?.trim();
            const companyFromSpan = root.querySelector('span')?.textContent?.trim();
            const companyFromHeading = root.querySelector('h4')?.textContent?.trim();
            const locationText = root.querySelector('small')?.textContent?.trim() || root.querySelector('time')?.textContent?.trim() || 'Unknown';
            const url = anchor?.href || `https://www.linkedin.com/jobs/view/${id}/`;

            return {
                url,
                title: titleFromStrong || titleFromHeading || 'Unknown title',
                company: companyFromHeading || companyFromSpan || 'Unknown company',
                location: locationText,
                easyApply: (root.textContent || '').toLowerCase().includes('easy apply') || false
            };
        }", jobId) ?? new LinkedInCardData();
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

    private sealed class LinkedInCardData
    {
        public string Url { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Company { get; init; } = string.Empty;
        public string Location { get; init; } = string.Empty;
        public bool EasyApply { get; init; }
    }
}