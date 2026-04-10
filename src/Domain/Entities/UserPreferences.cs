namespace jobAgentApi.Domain.Entities;

public class UserPreferences : AuditableEntity
{
    public Guid UserId { get; set; }
    public List<string> Skills { get; set; } = new();
    public string Level { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
}

public class SearchQuery : AuditableEntity
{
    public string Query { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = new();
    public string Level { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string NormalizedHash { get; set; } = string.Empty;
    public DateTime LastExecutedAt { get; set; } = DateTime.UtcNow;
}

public class UserSearchQuery
{
    public Guid UserId { get; set; }
    public Guid SearchQueryId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
