using jobAgentApi.Application.Abstractions.Messaging;

namespace jobAgentApi.Application.Features.User.Commands.GenerateCv;

public record GenerateCvCommand(Guid JobId, Guid UserId) : ICommand<GenerateCvResponse>;

public record GenerateCvResponse(byte[] PdfBytes, string FileName, string StorageUrl);
