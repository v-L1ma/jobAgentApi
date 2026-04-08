namespace jobAgentApi.Application.Abstractions;

public interface IJobCacheService
{
    Task<CachedJobSearchResult?> GetCachedResultAsync(string normalizedQuery, CancellationToken cancellationToken = default);
    Task SetCachedResultAsync(string normalizedQuery, CachedJobSearchResult result, CancellationToken cancellationToken = default);
    Task RemoveCachedResultAsync(string normalizedQuery, CancellationToken cancellationToken = default);
}

public interface IJobScrapingQueueService
{
    Task EnqueueScrapingRequestAsync(ScrapingQueueRequest request, CancellationToken cancellationToken = default);
    Task<ScrapingQueueStatus> GetStatusAsync(string normalizedQuery, CancellationToken cancellationToken = default);
    bool IsQueryInProgress(string normalizedQuery);
    Task<ScrapingQueueRequest?> TryDequeueAsync(CancellationToken cancellationToken = default);
    void MarkQueryCompleted(string normalizedQuery);
    void MarkQueryFailed(string normalizedQuery, Exception error);
}

public sealed record CachedJobSearchResult(
    IReadOnlyList<CachedJobItem> Jobs,
    DateTime CachedAtUtc,
    bool IsComplete);

public sealed record CachedJobItem(
    Guid Id,
    string Title,
    string Description,
    string Url,
    bool IsApplied,
    string? Company = null,
    string? Location = null,
    string? Platform = null);

public sealed record ScrapingQueueStatus(
    Guid? RequestId,
    bool IsRunning,
    DateTime? StartedAtUtc);

public sealed class JobCacheOptions
{
    public const string SectionName = "JobCache";
    public int TtlMinutes { get; set; } = 15;
    public int MaxCacheSize { get; set; } = 100;
}
