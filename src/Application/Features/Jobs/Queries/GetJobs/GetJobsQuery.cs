using jobAgentApi.Application.Abstractions.Messaging;

namespace jobAgentApi.Application.Features.Jobs.Queries.GetJobs;

public record GetJobsQuery(
    string? Stack = null,
    string? Location = null,
    string? Company = null,
    string? Platform = null,
    int Page = 1,
    int PageSize = 10,
    Guid? UserId = null,
    bool TriggerScraper = false) : IQuery<PagedJobsResponse>;

public record PagedJobsResponse(
    IEnumerable<JobListItemResponse> Items,
    int TotalItems,
    int CurrentPage,
    int TotalPages,
    ScraperTriggerResult? ScraperResult = null);

public record ScraperTriggerResult(
    bool Triggered,
    string Message,
    Guid? ExecutionId = null);

public record JobListItemResponse(
    Guid Id,
    string Title,
    string Description,
    string Url,
    bool IsApplied,
    string? Company = null,
    string? Platform = null);
