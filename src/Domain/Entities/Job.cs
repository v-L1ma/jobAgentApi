namespace jobAgentApi.Domain.Entities;

public class Job : AuditableEntity
{
    public string PlataformJobId { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool IsApplied { get; set; }
    public string Status { get; set; } = string.Empty;
    
    public JobAnalysis? Analysis { get; set; }
}