using jobAgentApi.Application.Abstractions.Messaging;

namespace jobAgentApi.Application.Features.Auth.Commands.ForgotPassword
{
    public record ForgotPasswordCommand(string Email) : ICommand<string>;
}