namespace jobAgentApi.Domain.Entities;

public class Questoes : AuditableEntity
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
}