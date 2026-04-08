namespace jobAgentApi.Domain.Entities;

public class UserCv : AuditableEntity
{
    public Guid UserId { get; set; }
    public string UrlFile { get; set; } = string.Empty;
    public string ExtractedText { get; set; } = string.Empty;

}
