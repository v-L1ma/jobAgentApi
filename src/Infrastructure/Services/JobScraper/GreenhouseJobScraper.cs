using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace jobAgentApi.Infrastructure.Services.JobScraper;

internal interface IGreenhouseJobScraper
{
    Task StreamJobsAsync(
        string query,
        string location,
        Func<GreenhouseScrapedJob, Task<bool>> onJob,
        CancellationToken cancellationToken);
}

internal sealed record GreenhouseScrapedJob(
    string Id,
    string Title,
    string Company,
    string Url,
    string Location,
    string? Description);

internal sealed class GreenhouseJobScraper : IGreenhouseJobScraper
{
    // List of known companies using Greenhouse
    private static readonly string[] GreenhouseCompanies = new[]
    {
        "nubank",
        "ifood",
        "stripe",
        "datadog",
        "notion",
        "figma",
        "netflix",
        "airbnb",
        "uber",
        "doordash",
        "brex",
        "plaid",
        "gusto",
        "inter",
        "stone",
        "mercado-livre",
        "b2rise",
        "contabilizei"
    };

    private readonly JobScraperOptions _options;
    private readonly ILogger<GreenhouseJobScraper> _logger;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public GreenhouseJobScraper(
        IOptions<JobScraperOptions> options,
        ILogger<GreenhouseJobScraper> logger,
        HttpClient? httpClient = null)
    {
        _options = options.Value;
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient();
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task StreamJobsAsync(
        string query,
        string location,
        Func<GreenhouseScrapedJob, Task<bool>> onJob,
        CancellationToken cancellationToken)
    {
        var desiredLocations = ParseDesiredLocations(location);
        var normalizedQuery = query.ToLowerInvariant().Trim();

        try
        {
            foreach (var company in GreenhouseCompanies)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await ProcessCompanyAsync(company, normalizedQuery, desiredLocations, onJob, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Greenhouse scraping for query {Query}", query);
            throw;
        }
    }

    private async Task ProcessCompanyAsync(
        string company,
        string normalizedQuery,
        List<string> desiredLocations,
        Func<GreenhouseScrapedJob, Task<bool>> onJob,
        CancellationToken cancellationToken)
    {
        var url = $"https://boards-api.greenhouse.io/v1/boards/{company}/jobs";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Greenhouse API returned {StatusCode} for company {Company}", 
                    response.StatusCode, company);
                return;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var data = JsonSerializer.Deserialize<GreenhouseResponse>(content, _jsonOptions);

            if (data?.Jobs is null || data.Jobs.Count == 0)
            {
                _logger.LogDebug("No jobs found for Greenhouse company {Company}", company);
                return;
            }

            _logger.LogInformation("Found {JobCount} jobs from Greenhouse company {Company}", 
                data.Jobs.Count, company);

            foreach (var job in data.Jobs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Filter by query
                    if (!MatchesQuery(job, normalizedQuery))
                        continue;

                    // Filter by location if specified
                    if (desiredLocations.Count > 0 && !MatchesLocation(job, desiredLocations))
                        continue;

                    var scraped = new GreenhouseScrapedJob(
                        job.Id.ToString(),
                        string.IsNullOrWhiteSpace(job.Title) ? "Unknown" : job.Title,
                        company,
                        job.AbsoluteUrl ?? string.Empty,
                        ExtractLocation(job),
                        null); // Description not extracted for efficiency

                    var shouldContinue = await onJob(scraped);
                    if (!shouldContinue)
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing Greenhouse job from {Company}", company);
                    continue;
                }
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "HTTP error fetching Greenhouse jobs for {Company}", company);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error processing Greenhouse company {Company}", company);
        }
    }

    private static bool MatchesQuery(GreenhouseJob job, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        var title = (job.Title ?? string.Empty).ToLowerInvariant();
        return title.Contains(query);
    }

    private static bool MatchesLocation(GreenhouseJob job, List<string> desiredLocations)
    {
        var location = ExtractLocation(job);
        var normalizedLocation = NormalizeLocationText(location);

        return desiredLocations.Any(loc => normalizedLocation.Contains(loc));
    }

    private static string ExtractLocation(GreenhouseJob job)
    {
        if (job.Offices is not null && job.Offices.Count > 0)
        {
            return string.Join(", ", job.Offices.Select(o => o.Name).Where(n => !string.IsNullOrWhiteSpace(n)));
        }

        return "Remote";
    }

    private static List<string> ParseDesiredLocations(string locationConfig)
    {
        return (locationConfig ?? string.Empty)
            .Split([';', ',', '|'], StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeLocationText)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    private static string NormalizeLocationText(string value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }

    private bool MatchesQuery(string query, string text)
    {
        return text.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    // DTO Classes
    private class GreenhouseResponse
    {
        [JsonPropertyName("jobs")]
        public List<GreenhouseJob> Jobs { get; set; } = new();
    }

    private class GreenhouseJob
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("absolute_url")]
        public string? AbsoluteUrl { get; set; }

        [JsonPropertyName("offices")]
        public List<GreenhouseOffice>? Offices { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }

    private class GreenhouseOffice
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
