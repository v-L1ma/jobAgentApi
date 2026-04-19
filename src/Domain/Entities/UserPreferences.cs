namespace jobAgentApi.Domain.Entities;

public class UserPreferences : AuditableEntity
{
    public Guid UserId { get; set; }
    public List<string> Skills { get; set; } = new();
    public List<string> Levels { get; set; } = new();
    public string Area { get; set; } = string.Empty;
}

public class SearchQuery : AuditableEntity
{
    public string Query { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = new();
    public List<string> Levels { get; set; } = new();
    public string Area { get; set; } = string.Empty;
    public string NormalizedHash { get; set; } = string.Empty;
    public DateTime LastExecutedAt { get; set; } = DateTime.MinValue;
}

public class UserSearchQuery
{
    public Guid UserId { get; set; }
    public Guid SearchQueryId { get; set; }
    public int SavedJobsCount { get; set; } = 0;
    public DateTime LimitedUntil { get; set; } = DateTime.UtcNow.AddHours(12);
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
