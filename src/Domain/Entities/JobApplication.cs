namespace jobAgentApi.Domain.Entities;
public class JobApplication : AuditableEntity
{
    public string JobId { get; set; } = string.Empty;
    public string JobUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public Questoes[] UnknownQuestions { get; set; } = [];
    public Questoes[] AnsweredQuestions { get; set; } = [];
    public DateTime TimeStamp { get; set; }

}