namespace jobAgentApi.Application.Abstractions;

public interface IJobScrapingQueueService
{
    Task EnqueueScrapingRequestAsync(ScrapingQueueRequest request, CancellationToken cancellationToken = default);
    Task<ScrapingQueueStatus> GetStatusAsync(string normalizedQuery, CancellationToken cancellationToken = default);
    bool IsQueryInProgress(string normalizedQuery);
    Task<ScrapingQueueRequest?> TryDequeueAsync(CancellationToken cancellationToken = default);
    void MarkQueryCompleted(string normalizedQuery);
    void MarkQueryFailed(string normalizedQuery, Exception error);
}

public sealed record ScrapingQueueStatus(
    Guid? RequestId,
    bool IsRunning,
    DateTime? StartedAtUtc);
