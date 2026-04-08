using jobAgentApi.Application.Abstractions.Messaging;

namespace jobAgentApi.Application.Features.Auth.Commands.Register
{
    public record RegisterCommand(string Name, string Email, string Password, string Role) : ICommand<Guid>;
}