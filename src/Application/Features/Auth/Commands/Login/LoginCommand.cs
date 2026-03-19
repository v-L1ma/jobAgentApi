using jobAgentApi.Application.Abstractions.Messaging;

namespace jobAgentApi.Application.Features.Auth.Commands
{
    public record LoginCommand(string Email, string Password) : ICommand<LoginCommandResponse>;
}
