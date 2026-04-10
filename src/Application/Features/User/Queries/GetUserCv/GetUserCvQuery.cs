using jobAgentApi.Application.Abstractions.Messaging;

namespace jobAgentApi.Application.Features.User.Queries.GetUserCv;

public record GetUserCvQuery(Guid UserId) : IQuery<GetUserCvResponse>;

public class GetUserCvResponse
{
    public Guid? UserCvId { get; set; }
    public string? UrlFile { get; set; }
    public byte[]? PdfBytes { get; set; }
    public string? FileName { get; set; }
    public DateTime? UploadedAtUtc { get; set; }
    public long? FileSizeBytes { get; set; }
    public bool HasCv { get; set; }
}
