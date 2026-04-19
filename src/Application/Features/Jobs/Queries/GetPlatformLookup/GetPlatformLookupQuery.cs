using jobAgentApi.Application.Abstractions.Messaging;
namespace jobAgentApi.Application.Features.Jobs.Queries.GetPlatformLookup;

public sealed record GetPlatformLookupQuery(
    Guid UserId,
    string? Search,
    int Limit = 20) : IQuery<PlatformLookupResponse>;

public sealed record PlatformLookupResponse(IReadOnlyList<string> Platforms);
