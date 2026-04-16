using jobAgentApi.Application.Abstractions.Messaging;

namespace jobAgentApi.Application.Features.User.Queries.GetUserProfile;

public sealed record GetUserProfileQuery(Guid UserId) : IQuery<GetUserProfileResponse>;

public sealed record GetUserProfileResponse(
    Guid Id,
    string Name,
    string Email,
    string? Cpf
);