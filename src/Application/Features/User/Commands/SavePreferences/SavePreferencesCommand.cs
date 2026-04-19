using jobAgentApi.Application.Abstractions.Messaging;

namespace jobAgentApi.Application.Features.User.Commands.SavePreferences;

public record SavePreferencesCommand(
    List<string> Skills,
    List<string> Levels,
    string Area,
    Guid UserId) : ICommand<Guid>;
