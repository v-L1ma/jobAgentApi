namespace jobAgentApi.Domain.Entities;

public class Plataform : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string AuthType { get; set; } = string.Empty;
}