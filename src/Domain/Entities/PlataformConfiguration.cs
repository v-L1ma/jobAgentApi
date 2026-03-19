namespace jobAgentApi.Domain.Entities;

public class PlataformConfiguration : AuditableEntity
{
    public Guid UserId { get; set; }
    public required Plataform Plataform { get; set; }
    public string AuthValue { get; set; } = string.Empty;
}