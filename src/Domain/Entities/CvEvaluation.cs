namespace jobAgentApi.Domain.Entities;

public class CvEvaluation : AuditableEntity
{
    public Guid UserId { get; set; }
    public Guid GeneratedCvId { get; set; }
    public bool Liked { get; set; }
    public string? Feedback { get; set; }

    public GeneratedCv GeneratedCv { get; set; } = null!;
}
