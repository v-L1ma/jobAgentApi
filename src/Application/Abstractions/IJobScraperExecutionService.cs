namespace jobAgentApi.Application.Abstractions;

public interface IJobScraperExecutionService
{
    bool IsRunning { get; }

    Task<JobScraperExecutionReport?> ExecuteAsync(
        JobScraperExecutionRequest request,
        CancellationToken cancellationToken);
}

public sealed record JobScraperExecutionRequest(
    string Trigger,
    Guid? UserId = null,
    Guid? SearchQueryId = null);

public sealed record JobScraperExecutionReport(
    Guid ExecutionId,
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    int QueriesProcessed,
    int JobsFound,
    int JobsSaved,
    int JobsSkipped,
    IReadOnlyList<string> Errors);