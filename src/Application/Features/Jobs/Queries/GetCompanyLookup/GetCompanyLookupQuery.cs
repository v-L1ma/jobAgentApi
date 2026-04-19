using jobAgentApi.Application.Abstractions.Messaging;

namespace jobAgentApi.Application.Features.Jobs.Queries.GetCompanyLookup;

public sealed record GetCompanyLookupQuery(
    Guid UserId,
    string? Search,
    int Limit = 20) : IQuery<CompanyLookupResponse>;

public sealed record CompanyLookupResponse(IReadOnlyList<string> Companies);
