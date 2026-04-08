using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace jobAgentApi.Infrastructure.Services.JobScraper;

internal interface IGuypJobScraper
{
    Task StreamJobsAsync(
        string query,
        string location,
        Func<GuypScrapedJob, Task<bool>> onJob,
        CancellationToken cancellationToken);
}

internal sealed record GuypScrapedJob(
    string Id,
    string Title,
    string Company,
    string Url,
    string Location,
    string? Description);

// DTOs for API Response
internal sealed class GuypJobApiResponse
{
    [JsonPropertyName("data")]
    public List<GuypJobData> Data { get; set; } = [];

    [JsonPropertyName("pagination")]
    public GuypJobPagination Pagination { get; set; } = new();
}

internal sealed class GuypJobData
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("careerPageName")]
    public string CareerPageName { get; set; } = string.Empty;

    [JsonPropertyName("jobUrl")]
    public string JobUrl { get; set; } = string.Empty;

    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("country")]
    public string Country { get; set; } = string.Empty;

    [JsonPropertyName("isRemoteWork")]
    public bool IsRemoteWork { get; set; }

    [JsonPropertyName("workplaceType")]
    public string WorkplaceType { get; set; } = string.Empty;
}

internal sealed class GuypJobPagination
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }
}

internal sealed class GuypJobScraper : IGuypJobScraper
{
    private const string ApiBaseUrl = "https://employability-portal.gupy.io/api/v1/jobs";
    private const string SortBy = "publishedDate";
    private const string SortOrder = "desc";

    private readonly JobScraperOptions _options;
    private readonly ILogger<GuypJobScraper> _logger;
    private readonly HttpClient _httpClient;

    public GuypJobScraper(
        IOptions<JobScraperOptions> options,
        ILogger<GuypJobScraper> logger,
        HttpClient httpClient)
    {
        _options = options.Value;
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task StreamJobsAsync(
        string query,
        string location,
        Func<GuypScrapedJob, Task<bool>> onJob,
        CancellationToken cancellationToken)
    {
        var searchKeywords = ParseSearchKeywords(query);
        var desiredLocations = ParseDesiredLocations(location);
        var processedJobIds = new HashSet<long>();
        var processedJobIdsLock = new object();

        if (searchKeywords.Count == 0)
        {
            _logger.LogWarning("No valid keywords parsed from query {Query}", query);
            return;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var stopRequested = 0;
        var maxParallelKeywords = Math.Min(3, searchKeywords.Count);

        try
        {
            await Parallel.ForEachAsync(
                searchKeywords,
                new ParallelOptions
                {
                    CancellationToken = linkedCts.Token,
                    MaxDegreeOfParallelism = maxParallelKeywords
                },
                async (keyword, ct) =>
                {
                    if (Volatile.Read(ref stopRequested) == 1)
                    {
                        return;
                    }

                    try
                    {
                        var shouldContinue = await StreamJobsForKeywordAsync(
                            keyword,
                            desiredLocations,
                            processedJobIds,
                            processedJobIdsLock,
                            onJob,
                            ct);

                        if (!shouldContinue && Interlocked.Exchange(ref stopRequested, 1) == 0)
                        {
                            linkedCts.Cancel();
                        }
                    }
                    catch (OperationCanceledException) when (Volatile.Read(ref stopRequested) == 1 && !cancellationToken.IsCancellationRequested)
                    {
                        // Expected when another keyword requested stop.
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Gupy keyword search failed for {Keyword}. Continuing with remaining keywords.", keyword);
                    }
                });
        }
        catch (OperationCanceledException) when (Volatile.Read(ref stopRequested) == 1 && !cancellationToken.IsCancellationRequested)
        {
            // Expected when one keyword stream requests stop (e.g., max jobs reached).
        }
        finally
        {
            if (Volatile.Read(ref stopRequested) == 1)
            {
                _logger.LogInformation("Stopping Gupy keyword searches because scraper callback requested interruption");
            }
        }
    }

    private async Task<bool> StreamJobsForKeywordAsync(
        string keyword,
        List<string> desiredLocations,
        HashSet<long> processedJobIds,
        object processedJobIdsLock,
        Func<GuypScrapedJob, Task<bool>> onJob,
        CancellationToken cancellationToken)
    {
        int offset = 0;
        var jobsProcessed = 0;
        var maxJobsPerKeyword = _options.MaxJobsPerQuery;

        while (jobsProcessed < maxJobsPerKeyword)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var apiUrl = BuildApiUrl(keyword, _options.MaxJobsPerQuery, offset);
                _logger.LogInformation("Calling Gupy API for keyword {Keyword} with offset {Offset}", keyword, offset);

                var response = await _httpClient.GetAsync(apiUrl, cancellationToken);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var apiResponse = System.Text.Json.JsonSerializer.Deserialize<GuypJobApiResponse>(content) ?? new GuypJobApiResponse();

                if (apiResponse.Data.Count == 0)
                {
                    _logger.LogInformation("No more jobs found for keyword {Keyword} at offset {Offset}", keyword, offset);
                    break;
                }

                foreach (var jobData in apiResponse.Data)
                {
                    if (jobsProcessed >= maxJobsPerKeyword)
                    {
                        return false; // Max jobs reached, signal to stop
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    if (!TryRegisterProcessedJob(processedJobIds, processedJobIdsLock, jobData.Id))
                    {
                        continue; // Job already processed
                    }

                    // Build location string
                    var jobLocation = BuildLocationString(jobData.City, jobData.State, jobData.Country, jobData.IsRemoteWork);

                    // Validate location if filters are specified
                    var normalizedLocation = NormalizeText(jobLocation);
                    var isValidLocation = desiredLocations.Count == 0 ||
                                        desiredLocations.Any(loc => normalizedLocation.Contains(loc));

                    if (!isValidLocation && desiredLocations.Count > 0)
                    {
                        _logger.LogDebug("Job location {Location} doesn't match search filters", jobLocation);
                        continue;
                    }

                    var job = new GuypScrapedJob(
                        jobData.Id.ToString(),
                        string.IsNullOrWhiteSpace(jobData.Name) ? "Unknown" : jobData.Name,
                        string.IsNullOrWhiteSpace(jobData.CareerPageName) ? "Unknown" : jobData.CareerPageName,
                        jobData.JobUrl,
                        jobLocation,
                        jobData.Description);

                    var shouldContinue = await onJob(job);
                    jobsProcessed++;

                    if (!shouldContinue)
                    {
                        return false; // Callback requested stop
                    }

                    await RandomDelayAsync(cancellationToken);
                }

                offset += _options.MaxJobsPerQuery;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error calling Gupy API for keyword {Keyword}", keyword);
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error calling Gupy API for keyword {Keyword}", keyword);
                throw;
            }
        }

        return true;
    }

    private static string BuildApiUrl(string keyword, int limit, int offset)
    {
        var encodedKeyword = Uri.EscapeDataString(keyword);
        return $"{ApiBaseUrl}?jobName={encodedKeyword}&limit={limit}&offset={offset}&sortBy={SortBy}&sortOrder={SortOrder}";
    }

    private static string BuildLocationString(string city, string state, string country, bool isRemote)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(city))
            parts.Add(city);

        if (!string.IsNullOrWhiteSpace(state))
            parts.Add(state);

        if (!string.IsNullOrWhiteSpace(country))
            parts.Add(country);

        if (isRemote && !parts.Any(p => p.Equals("remote", StringComparison.OrdinalIgnoreCase)))
            parts.Add("Remote");

        return parts.Count > 0 ? string.Join(", ", parts) : "Unknown";
    }

    private static List<string> ParseSearchKeywords(string query)
    {
        var sanitizedQuery = (query ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(sanitizedQuery))
        {
            return [];
        }

        var keywordSection = sanitizedQuery;
        var andIndex = sanitizedQuery.IndexOf(" AND ", StringComparison.OrdinalIgnoreCase);
        if (andIndex >= 0)
        {
            keywordSection = sanitizedQuery[..andIndex];
        }

        keywordSection = keywordSection.Trim().Trim('(', ')');
        if (string.IsNullOrWhiteSpace(keywordSection))
        {
            return [];
        }

        var parsedKeywords = keywordSection
            .Split(" OR ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.Trim().Trim('(', ')'))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return parsedKeywords.Count > 0 ? parsedKeywords : [keywordSection];
    }

    private static bool TryRegisterProcessedJob(HashSet<long> processedJobIds, object syncLock, long jobId)
    {
        lock (syncLock)
        {
            return processedJobIds.Add(jobId);
        }
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
            return string.Empty;

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

    private async Task RandomDelayAsync(CancellationToken cancellationToken)
    {
        if (_options.MaxDelayMs <= 0)
            return;

        var min = Math.Max(0, _options.MinDelayMs);
        var max = Math.Max(min, _options.MaxDelayMs);
        var delay = Random.Shared.Next(min, max + 1);
        await Task.Delay(delay, cancellationToken);
    }
}
