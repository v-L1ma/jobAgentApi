using jobAgentApi.Application.Abstractions.Messaging;

namespace jobAgentApi.Application.Features.Auth.Commands.Login
{
    public record LoginCommandResponse(string Token, string RefreshToken);
}
