namespace jobAgentApi.Domain.Entities;

public class JobAnalysis : AuditableEntity
{
    public Guid JobId { get; set; }
    public string Skills { get; set; } = string.Empty;
    public string Nivel { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;
}