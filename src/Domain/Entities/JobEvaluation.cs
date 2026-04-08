namespace jobAgentApi.Domain.Entities;

public class JobEvaluation : AuditableEntity
{
    public Guid UserId { get; set; }
    public Guid JobId { get; set; }
    public bool Liked { get; set; }
    public string? Feedback { get; set; }

    public Job Job { get; set; } = null!;
}
