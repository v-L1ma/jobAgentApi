namespace jobAgentApi.Application.Features.Jobs.Commands.RunJobScraper;

public sealed record RunJobScraperResponse(
    Guid ExecutionId,
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    int QueriesProcessed,
    int JobsFound,
    int JobsSaved,
    int JobsSkipped,
    IReadOnlyList<string> Errors);