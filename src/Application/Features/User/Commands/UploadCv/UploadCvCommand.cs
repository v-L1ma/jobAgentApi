using jobAgentApi.Application.Abstractions.Messaging;

namespace jobAgentApi.Application.Features.User.Commands.UploadCv;

public record UploadCvCommand(Stream FileStream, string FileName, string ContentType, Guid UserId) : ICommand<string>;
