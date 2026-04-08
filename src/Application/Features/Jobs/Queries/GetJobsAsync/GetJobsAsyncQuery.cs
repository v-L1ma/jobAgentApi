using jobAgentApi.Application.Abstractions.Messaging;

namespace jobAgentApi.Application.Features.Jobs.Queries.GetJobsAsync;

public sealed record GetJobsAsyncQuery(
    string? Query = null,
    string? Stack = null,
    string? Location = null,
    int Page = 1,
    int PageSize = 10,
    Guid? UserId = null) : IQuery<JobSearchResponse>;

public sealed record JobSearchResponse(
    string Status,
    bool IsLoading,
    IReadOnlyList<JobItemResponse> Data,
    JobSearchMeta Meta);

public sealed record JobSearchMeta(
    bool ScraperRunning,
    bool FromCache,
    int? TotalItems = null,
    int? CurrentPage = null,
    int? TotalPages = null,
    Guid? RequestId = null);

public sealed record JobItemResponse(
    Guid Id,
    string Title,
    string Description,
    string Url,
    bool IsApplied,
    string? Company = null,
    string? Location = null,
    string? Platform = null,
    DateTime? CreatedAt = null);
