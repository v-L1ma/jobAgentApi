using jobAgentApi.Application.Abstractions.Messaging;

namespace jobAgentApi.Application.Features.Jobs.Queries.TriggerScraper;

public sealed record TriggerScraperOnGetJobsQuery(Guid UserId) : IQuery<TriggerScraperResult>;

public sealed record TriggerScraperResult(
    bool ScraperTriggered,
    string? Message,
    Guid? ExecutionId = null);
