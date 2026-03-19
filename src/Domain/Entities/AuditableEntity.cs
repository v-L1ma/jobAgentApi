namespace jobAgentApi.Domain.Entities;
public class AuditableEntity
{
    public Guid Id { get; set; }
    public bool Active { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } 
    public string LastModifiedBy { get; set; } = string.Empty;
    public DateTime LastModifiedAt { get; set; } 
}