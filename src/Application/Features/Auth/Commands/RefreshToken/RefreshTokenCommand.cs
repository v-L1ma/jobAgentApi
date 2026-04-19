using jobAgentApi.Application.Abstractions.Messaging;

namespace jobAgentApi.Application.Features.Auth.Commands.RefreshToken
{
    public record RefreshTokenCommand(string Token, string RefreshToken) : ICommand<RefreshTokenResponse>;

    public record RefreshTokenResponse(string Token, string RefreshToken, bool IsFirstAccess);
}