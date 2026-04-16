using jobAgentApi.Application.Abstractions.Messaging;

namespace jobAgentApi.Application.Features.User.Queries.GetGeneratedCvs;

public record GetGeneratedCvsQuery(Guid UserId) : IQuery<GetGeneratedCvsResponse>;

public class GetGeneratedCvsResponse
{
    public List<GeneratedCvItemDto> Items { get; set; } = new();
    public int Total { get; set; }
}

public class GeneratedCvItemDto
{
    public Guid Id { get; set; }
    public string UrlFile { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string FileName { get; set; } = string.Empty;
}
