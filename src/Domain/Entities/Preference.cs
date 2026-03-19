namespace jobAgentApi.Domain.Entities;

public class Preference : AuditableEntity
{
    public Guid UserId { get; set; }
    public string[] ExcludeKeywords { get; set; } = [];
    public string[] IncludeKeywords { get; set; } = [];
    public string? Location { get; set; } = string.Empty; //Se a location for nula aceita qualquer uma
}