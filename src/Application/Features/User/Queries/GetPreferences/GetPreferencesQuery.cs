using jobAgentApi.Application.Abstractions.Messaging;

namespace jobAgentApi.Application.Features.User.Queries.GetPreferences;

public record GetPreferencesQuery(Guid UserId) : IQuery<UserPreferencesDto>;

public class UserPreferencesDto
{
    public Guid UserId { get; set; }
    public List<string> Skills { get; set; } = new();
    public List<string> Levels { get; set; } = new();
    public string Area { get; set; } = string.Empty;
}
