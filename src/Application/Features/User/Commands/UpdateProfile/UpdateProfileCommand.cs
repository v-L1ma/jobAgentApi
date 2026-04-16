using jobAgentApi.Application.Abstractions.Messaging;

namespace jobAgentApi.Application.Features.User.Commands.UpdateProfile;

public record UpdateProfileCommand(
    Guid UserId,
    string? Name,
    string? Email,
    string? CurrentPassword,
    string? NewPassword,
    string? ConfirmNewPassword) : ICommand<Guid>;
