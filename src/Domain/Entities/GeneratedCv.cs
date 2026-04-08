namespace jobAgentApi.Domain.Entities;

public class GeneratedCv : AuditableEntity
{
    public Guid UserId { get; set; }
    public string UrlFile { get; set; } = string.Empty;
}