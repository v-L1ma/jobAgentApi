using jobAgentApi.Application.Abstractions;
using jobAgentApi.Application.Repositories;
using jobAgentApi.Domain.Entities;
using jobAgentApi.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace jobAgentApi.Infrastructure.Services.JobScraper;

internal sealed class JobScraperExecutionService : IJobScraperExecutionService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILinkedInJobScraper _linkedInJobScraper;
    private readonly IGuypJobScraper _guypJobScraper;
    private readonly IGreenhouseJobScraper _greenhouseJobScraper;
    private readonly IVagasComBrJobScraper _vagasComBrJobScraper;
    private readonly IOptions<JobScraperOptions> _options;
    private readonly ILogger<JobScraperExecutionService> _logger;
    private readonly SemaphoreSlim _executionLock = new(1, 1);
    private readonly SemaphoreSlim _saveJobLock = new(1, 1);

    public JobScraperExecutionService(
        IServiceScopeFactory scopeFactory,
        ILinkedInJobScraper linkedInJobScraper,
        IGuypJobScraper guypJobScraper,
        IGreenhouseJobScraper greenhouseJobScraper,
        IVagasComBrJobScraper vagasComBrJobScraper,
        IOptions<JobScraperOptions> options,
        ILogger<JobScraperExecutionService> logger)
    {
        _scopeFactory = scopeFactory;
        _linkedInJobScraper = linkedInJobScraper;
        _guypJobScraper = guypJobScraper;
        _greenhouseJobScraper = greenhouseJobScraper;
        _vagasComBrJobScraper = vagasComBrJobScraper;
        _options = options;
        _logger = logger;
    }

    public bool IsRunning => _executionLock.CurrentCount == 0;

    public async Task<JobScraperExecutionReport?> ExecuteAsync(
        JobScraperExecutionRequest request,
        CancellationToken cancellationToken)
    {
        var lockAcquired = await _executionLock.WaitAsync(0, cancellationToken);
        if (!lockAcquired)
        {
            return null;
        }

        var executionId = Guid.NewGuid();
        var startedAtUtc = DateTime.UtcNow;
        var errors = new List<string>();
        var counters = new QueryExecutionCounters();

        try
        {
            using var executionScope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["executionId"] = executionId,
                ["trigger"] = request.Trigger
            });

            _logger.LogInformation("Starting scraper execution with trigger {Trigger}", request.Trigger);

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var jobRepository = scope.ServiceProvider.GetRequiredService<IJobRepository>();

            var queryContexts = await LoadQueryContextsAsync(dbContext, request, cancellationToken);
            var userDailyStates = BuildUserDailyStates(queryContexts, _options.Value.MaxApplicationsPerDay);
            var savedJobsByUserSearchQuery = new Dictionary<(Guid UserId, Guid SearchQueryId), int>();
            if (queryContexts.Count == 0)
            {
                _logger.LogInformation("No active user queries found to process");
            }

            foreach (var context in queryContexts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!CanSaveForUser(context.UserId, userDailyStates, out var limitedUntil))
                {
                    _logger.LogInformation(
                        "Skipping query {SearchQueryId} because user {UserId} is limited until {LimitedUntil}",
                        context.SearchQueryId,
                        context.UserId,
                        limitedUntil);
                    continue;
                }

                counters.TotalQueriesProcessed++;

                using var queryScope = _logger.BeginScope(new Dictionary<string, object>
                {
                    ["queryId"] = context.SearchQueryId,
                    ["userId"] = context.UserId
                });

                _logger.LogInformation("Processing query {QueryText} for user {UserId}",
                    context.Query, context.UserId);

                var queryState = new QueryState();

                try
                {
                    // Gupy - limite por query
                    _logger.LogWarning("Scraper start: Gupy for query {SearchQueryId}", context.SearchQueryId);
                    await RunGuypQueryWithRetryAsync(
                        context,
                        job => SaveJobAsync(
                            jobRepository,
                            context,
                            queryState,
                            counters,
                            userDailyStates,
                            Platform.Gupy,
                            job.Id,
                            job.Title,
                            job.Company,
                            job.Url,
                            job.Description,
                            cancellationToken),
                        cancellationToken);
                    _logger.LogWarning(
                        "Scraper end: Gupy for query {SearchQueryId}. SavedOnPlatform={SavedOnPlatform}",
                        context.SearchQueryId,
                        counters.GetJobsAddedCount(context.SearchQueryId, Platform.Gupy));

                    // Greenhouse - limite por query
                    // await RunGreenhouseQueryWithRetryAsync(
                    //     context,
                    //     job => SaveJobAsync(
                    //         jobRepository,
                    //         context,
                    //         queryState,
                    //         counters,
                    //         userDailyStates,
                    //         Platform.Greenhouse,
                    //         job.Id,
                    //         job.Title,
                    //         job.Url,
                    //         job.Description,
                    //         cancellationToken),
                    //     cancellationToken);

                    // Vagas.com.br - limite por query
                    _logger.LogWarning("Scraper start: VagasComBr for query {SearchQueryId}", context.SearchQueryId);
                    await RunVagasComBrQueryWithRetryAsync(
                        context,
                        job => SaveJobAsync(
                            jobRepository,
                            context,
                            queryState,
                            counters,
                            userDailyStates,
                            Platform.VagasComBr,
                            job.Id,
                            job.Title,
                            job.Company,
                            job.Url,
                            job.Description,
                            cancellationToken),
                        cancellationToken);
                    _logger.LogWarning(
                        "Scraper end: VagasComBr for query {SearchQueryId}. SavedOnPlatform={SavedOnPlatform}",
                        context.SearchQueryId,
                        counters.GetJobsAddedCount(context.SearchQueryId, Platform.VagasComBr));

                    // LinkedIn - limite por query
                    _logger.LogWarning("Scraper start: LinkedIn for query {SearchQueryId}", context.SearchQueryId);
                    await RunLinkedInQueryWithRetryAsync(
                        context,
                        job => SaveJobAsync(
                            jobRepository,
                            context,
                            queryState,
                            counters,
                            userDailyStates,
                            Platform.LinkedIn,
                            job.Id,
                            job.Title,
                            job.Company,
                            job.Url,
                            job.Description,
                            cancellationToken),
                        cancellationToken);
                    _logger.LogWarning(
                        "Scraper end: LinkedIn for query {SearchQueryId}. SavedOnPlatform={SavedOnPlatform}",
                        context.SearchQueryId,
                        counters.GetJobsAddedCount(context.SearchQueryId, Platform.LinkedIn));

                }
                catch (Exception ex)
                {
                    var message = $"Query {context.SearchQueryId} failed: {ex.Message}";
                    errors.Add(message);
                    _logger.LogError(ex, "Query processing failed");
                }
                finally
                {
                    savedJobsByUserSearchQuery[(context.UserId, context.SearchQueryId)] = queryState.SavedCount;
                }
            }

            await UpdateUserSearchQueryLimitsAsync(
                dbContext,
                queryContexts,
                userDailyStates,
                savedJobsByUserSearchQuery,
                cancellationToken);
        }
        catch (Exception ex)
        {
            errors.Add($"Fatal execution error: {ex.Message}");
            _logger.LogError(ex, "Fatal scraper execution error");
        }
        finally
        {
            _executionLock.Release();
        }

        var finishedAtUtc = DateTime.UtcNow;
        return new JobScraperExecutionReport(
            executionId,
            startedAtUtc,
            finishedAtUtc,
            counters.TotalQueriesProcessed,
            counters.TotalJobsFound,
            counters.TotalJobsSaved,
            counters.TotalJobsSkipped,
            errors);
    }

    private async Task<bool> SaveJobAsync(
        IJobRepository jobRepository,
        QueryExecutionContext context,
        QueryState queryState,
        QueryExecutionCounters counters,
        Dictionary<Guid, UserDailyState> userDailyStates,
        Platform platform,
        string jobId,
        string title,
        string? company,
        string url,
        string? description,
        CancellationToken cancellationToken)
    {
        await _saveJobLock.WaitAsync(cancellationToken);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            counters.MarkFound();

            using var jobScope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["jobId"] = jobId
            });

            // Check per-query + platform limit (skipped jobs don't count)
            var limit = platform switch
            {
                Platform.LinkedIn => _options.Value.MaxLinkedInJobsPerQuery,
                Platform.Gupy => _options.Value.MaxGupyJobsPerQuery,
                Platform.Greenhouse => _options.Value.MaxGreenhouseJobsPerQuery,
                Platform.VagasComBr => _options.Value.MaxVagasComBrJobsPerQuery,
                _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null)
            };

            var canAdd = counters.TryAddJob(context.SearchQueryId, platform, limit);
            if (!canAdd)
            {
                _logger.LogWarning(
                    "Limite de {Limit} vagas atingido para query '{QueryText}' na plataforma {Platform}. Parando esta query.",
                    limit, context.Query, platform);
                return false;
            }

            // Check global execution limit (safety net)
            if (counters.TotalJobsSaved >= _options.Value.MaxJobsPerExecution)
            {
                _logger.LogWarning("Execution global limit reached ({SavedCount}/{MaxCount}). Stopping scraping.",
                    counters.TotalJobsSaved, _options.Value.MaxJobsPerExecution);
                return false;
            }

            if (!CanSaveForUser(context.UserId, userDailyStates, out var limitedUntil))
            {
                _logger.LogWarning(
                    "Stopping query because user {UserId} is limited until {LimitedUntil}",
                    context.UserId,
                    limitedUntil);
                return false;
            }

            var existingJob = await jobRepository.GetByPlataformJobIdOrUrlAsync(jobId, url, cancellationToken);

            if (existingJob is not null)
            {
                if (string.IsNullOrWhiteSpace(existingJob.Platform))
                {
                    existingJob.Platform = platform.ToString();
                }

                if (string.IsNullOrWhiteSpace(existingJob.Company) && !string.IsNullOrWhiteSpace(company))
                {
                    existingJob.Company = company.Trim();
                }

                counters.MarkSkipped();
                _logger.LogWarning(
                    "[{Platform}] Job SKIPPED reason=already_exists jobId={JobId} title={Title} url={Url}",
                    platform,
                    jobId,
                    title,
                    url);
                return true; // Continua, mas nao conta no limite
            }

            if (context.ExcludeKeywords.Any(keyword =>
                    !string.IsNullOrWhiteSpace(keyword) &&
                    title.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                counters.MarkSkipped();
                _logger.LogWarning(
                    "[{Platform}] Job SKIPPED reason=excluded_keyword jobId={JobId} title={Title}",
                    platform,
                    jobId,
                    title);
                return true; // Continua, mas nao conta no limite
            }

            var newJob = new Job
            {
                Id = Guid.NewGuid(),
                PlataformJobId = jobId,
                Platform = platform.ToString(),
                Company = string.IsNullOrWhiteSpace(company) ? "Empresa não informada" : company.Trim(),
                Title = title,
                Description = description ?? string.Empty,
                Url = url,
                Status = "saved",
                IsApplied = false,
                Active = true,
                CreatedBy = "job-scraper",
                LastModifiedBy = "job-scraper",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            };

            await jobRepository.AddAsync(newJob, cancellationToken);
            await jobRepository.SaveChangesAsync(cancellationToken);

            queryState.SavedCount++;

            var userState = userDailyStates[context.UserId];
            userState.SavedJobsCount++;

            if (userState.SavedJobsCount > _options.Value.MaxApplicationsPerDay)
            {
                userState.LimitedUntil = DateTime.UtcNow.AddHours(12);
                _logger.LogWarning(
                    "User {UserId} exceeded daily limit ({SavedJobsCount}/{Limit}) and is limited until {LimitedUntil}",
                    context.UserId,
                    userState.SavedJobsCount,
                    _options.Value.MaxApplicationsPerDay,
                    userState.LimitedUntil);
            }

            counters.MarkSaved();
            _logger.LogWarning(
                "[{Platform}] Job ADDED jobId={JobId} title={Title} url={Url}",
                platform,
                jobId,
                title,
                url);

            if (userState.SavedJobsCount > _options.Value.MaxApplicationsPerDay)
            {
                return false;
            }

            return true;
        }
        finally
        {
            _saveJobLock.Release();
        }
    }

    private async Task<List<QueryExecutionContext>> LoadQueryContextsAsync(
        AppDbContext dbContext,
        JobScraperExecutionRequest request,
        CancellationToken cancellationToken)
    {
        var query =
            from usq in dbContext.UserSearchQueries.AsNoTracking()
            join sq in dbContext.SearchQueries.AsNoTracking() on usq.SearchQueryId equals sq.Id
            where sq.Active
            select new
            {
                usq.UserId,
                usq.SearchQueryId,
                usq.SavedJobsCount,
                usq.LimitedUntil,
                sq.Query
            };

        if (request.UserId.HasValue)
        {
            query = query.Where(item => item.UserId == request.UserId.Value);
        }

        if (request.SearchQueryId.HasValue)
        {
            query = query.Where(item => item.SearchQueryId == request.SearchQueryId.Value);
        }

        var baseRows = await query.ToListAsync(cancellationToken);
        if (baseRows.Count == 0)
        {
            return [];
        }

        var userIds = baseRows.Select(row => row.UserId).Distinct().ToList();
        var preferences = await dbContext.Preferences.AsNoTracking()
            .Where(p => userIds.Contains(p.UserId) && p.Active)
            .ToListAsync(cancellationToken);

        var contexts = new List<QueryExecutionContext>(baseRows.Count);
        foreach (var row in baseRows)
        {
            var preference = preferences.FirstOrDefault(item => item.UserId == row.UserId);
            contexts.Add(new QueryExecutionContext(
                row.UserId,
                row.SearchQueryId,
                row.Query,
                preference?.ExcludeKeywords ?? [],
                preference?.Location ?? string.Empty,
                row.SavedJobsCount,
                row.LimitedUntil));
        }

        return contexts;
    }

    private static Dictionary<Guid, UserDailyState> BuildUserDailyStates(
        IEnumerable<QueryExecutionContext> queryContexts,
        int maxApplicationsPerDay)
    {
        var now = DateTime.UtcNow;
        var result = new Dictionary<Guid, UserDailyState>();

        foreach (var group in queryContexts.GroupBy(context => context.UserId))
        {
            var totalCount = group.Sum(context => context.SavedJobsCount);
            var limitedUntil = group.Max(context => context.LimitedUntil);
            var shouldResetCount = limitedUntil <= now && totalCount > maxApplicationsPerDay;

            result[group.Key] = new UserDailyState
            {
                SavedJobsCount = shouldResetCount ? 0 : totalCount,
                LimitedUntil = shouldResetCount ? DateTime.MinValue : limitedUntil,
                ShouldResetCount = shouldResetCount
            };
        }

        return result;
    }

    private bool CanSaveForUser(
        Guid userId,
        Dictionary<Guid, UserDailyState> userDailyStates,
        out DateTime? limitedUntil)
    {
        limitedUntil = null;

        if (!userDailyStates.TryGetValue(userId, out var userState))
        {
            return true;
        }

        var now = DateTime.UtcNow;

        if (userState.SavedJobsCount > _options.Value.MaxApplicationsPerDay)
        {
            if (userState.LimitedUntil > now)
            {
                limitedUntil = userState.LimitedUntil;
                return false;
            }

            userState.SavedJobsCount = 0;
            userState.LimitedUntil = DateTime.MinValue;
            userState.ShouldResetCount = true;
        }

        return true;
    }

    private async Task UpdateUserSearchQueryLimitsAsync(
        AppDbContext dbContext,
        List<QueryExecutionContext> queryContexts,
        Dictionary<Guid, UserDailyState> userDailyStates,
        Dictionary<(Guid UserId, Guid SearchQueryId), int> savedJobsByUserSearchQuery,
        CancellationToken cancellationToken)
    {
        if (queryContexts.Count == 0)
        {
            return;
        }

        var userIds = queryContexts.Select(context => context.UserId).Distinct().ToList();
        var userSearchQueries = await dbContext.UserSearchQueries
            .Where(item => userIds.Contains(item.UserId))
            .ToListAsync(cancellationToken);

        var hasChanges = false;
        var now = DateTime.UtcNow;

        foreach (var userSearchQuery in userSearchQueries)
        {
            if (!userDailyStates.TryGetValue(userSearchQuery.UserId, out var userState))
            {
                continue;
            }

            if (userState.ShouldResetCount && userSearchQuery.SavedJobsCount != 0)
            {
                userSearchQuery.SavedJobsCount = 0;
                hasChanges = true;
            }

            if (savedJobsByUserSearchQuery.TryGetValue((userSearchQuery.UserId, userSearchQuery.SearchQueryId), out var savedJobs)
                && savedJobs > 0)
            {
                userSearchQuery.SavedJobsCount += savedJobs;
                hasChanges = true;
            }

            if (userState.SavedJobsCount > _options.Value.MaxApplicationsPerDay && userState.LimitedUntil > now)
            {
                if (userSearchQuery.LimitedUntil != userState.LimitedUntil)
                {
                    userSearchQuery.LimitedUntil = userState.LimitedUntil;
                    hasChanges = true;
                }
            }
            else if (userState.ShouldResetCount && userSearchQuery.LimitedUntil != DateTime.MinValue)
            {
                userSearchQuery.LimitedUntil = DateTime.MinValue;
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task RunLinkedInQueryWithRetryAsync(
        QueryExecutionContext context,
        Func<LinkedInScrapedJob, Task<bool>> onJob,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= _options.Value.RetryCount; attempt++)
        {
            try
            {
                await _linkedInJobScraper.StreamJobsAsync(
                    context.Query,
                    context.Location,
                    ParseLiAtCookie(_options.Value.LiAtCookie),
                    _options.Value.EasyApplyOnly,
                    onJob,
                    cancellationToken);

                return;
            }
            catch (Exception ex) when (ex is TimeoutException or PlaywrightException)
            {
                if (attempt >= _options.Value.RetryCount)
                {
                    await SaveFailureScreenshotAsync(context.SearchQueryId, cancellationToken);
                    throw;
                }

                var delay = _options.Value.RetryBaseDelayMs * (int)Math.Pow(2, attempt);
                _logger.LogWarning(
                    ex,
                    "Transient failure while scraping LinkedIn query {SearchQueryId}. Retry attempt {Attempt}",
                    context.SearchQueryId,
                    attempt + 1);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private async Task RunGuypQueryWithRetryAsync(
        QueryExecutionContext context,
        Func<GuypScrapedJob, Task<bool>> onJob,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= _options.Value.RetryCount; attempt++)
        {
            try
            {
                await _guypJobScraper.StreamJobsAsync(
                    context.Query,
                    context.Location,
                    onJob,
                    cancellationToken);

                return;
            }
            catch (Exception ex) when (ex is TimeoutException or PlaywrightException)
            {
                if (attempt >= _options.Value.RetryCount)
                {
                    await SaveFailureScreenshotAsync(context.SearchQueryId, cancellationToken);
                    throw;
                }

                var delay = _options.Value.RetryBaseDelayMs * (int)Math.Pow(2, attempt);
                _logger.LogWarning(
                    ex,
                    "Transient failure while scraping Gupy query {SearchQueryId}. Retry attempt {Attempt}",
                    context.SearchQueryId,
                    attempt + 1);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private async Task RunGreenhouseQueryWithRetryAsync(
        QueryExecutionContext context,
        Func<GreenhouseScrapedJob, Task<bool>> onJob,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= _options.Value.RetryCount; attempt++)
        {
            try
            {
                await _greenhouseJobScraper.StreamJobsAsync(
                    context.Query,
                    context.Location,
                    onJob,
                    cancellationToken);

                return;
            }
            catch (Exception ex) when (ex is TimeoutException or HttpRequestException or TaskCanceledException)
            {
                if (attempt >= _options.Value.RetryCount)
                {
                    await SaveFailureScreenshotAsync(context.SearchQueryId, cancellationToken);
                    throw;
                }

                var delay = _options.Value.RetryBaseDelayMs * (int)Math.Pow(2, attempt);
                _logger.LogWarning(
                    ex,
                    "Transient failure while scraping Greenhouse query {SearchQueryId}. Retry attempt {Attempt}",
                    context.SearchQueryId,
                    attempt + 1);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private async Task RunVagasComBrQueryWithRetryAsync(
        QueryExecutionContext context,
        Func<VagasComBrScrapedJob, Task<bool>> onJob,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= _options.Value.RetryCount; attempt++)
        {
            try
            {
                await _vagasComBrJobScraper.StreamJobsAsync(
                    context.Query,
                    context.Location,
                    onJob,
                    cancellationToken);

                return;
            }
            catch (Exception ex) when (ex is TimeoutException or PlaywrightException or HttpRequestException or TaskCanceledException)
            {
                if (attempt >= _options.Value.RetryCount)
                {
                    await SaveFailureScreenshotAsync(context.SearchQueryId, cancellationToken);
                    throw;
                }

                var delay = _options.Value.RetryBaseDelayMs * (int)Math.Pow(2, attempt);
                _logger.LogWarning(
                    ex,
                    "Transient failure while scraping Vagas.com.br query {SearchQueryId}. Retry attempt {Attempt}",
                    context.SearchQueryId,
                    attempt + 1);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private async Task SaveFailureScreenshotAsync(Guid searchQueryId, CancellationToken cancellationToken)
    {
        try
        {
            var pathRoot = Path.IsPathRooted(_options.Value.ScreenshotsPath)
                ? _options.Value.ScreenshotsPath
                : Path.Combine(AppContext.BaseDirectory, _options.Value.ScreenshotsPath);

            Directory.CreateDirectory(pathRoot);

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[]
                {
                    "--no-sandbox",
                    "--disable-setuid-sandbox"
                }
            });

            await using var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();
            await page.GotoAsync("about:blank");

            var fileName = $"scraper-fatal-{searchQueryId:N}-{DateTime.UtcNow:yyyyMMddHHmmss}.png";
            var fullPath = Path.Combine(pathRoot, fileName);

            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = fullPath,
                FullPage = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not save fatal screenshot for query {SearchQueryId}", searchQueryId);
        }
    }

    private static string? ParseLiAtCookie(string? configuredValue)
    {
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return null;
        }

        var trimmed = configuredValue.Trim();

        const string cookiePrefix = "li_at=";
        var index = trimmed.IndexOf(cookiePrefix, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            var valueStart = index + cookiePrefix.Length;
            var tail = trimmed[valueStart..];
            var end = tail.IndexOf(';');
            return (end >= 0 ? tail[..end] : tail).Trim();
        }

        return trimmed;
    }

    private sealed record QueryExecutionContext(
        Guid UserId,
        Guid SearchQueryId,
        string Query,
        string[] ExcludeKeywords,
        string Location,
        int SavedJobsCount,
        DateTime LimitedUntil);

    private sealed class UserDailyState
    {
        public int SavedJobsCount { get; set; }
        public DateTime LimitedUntil { get; set; }
        public bool ShouldResetCount { get; set; }
    }

    private sealed class QueryState
    {
        public int SavedCount { get; set; }
    }
}