using jobAgentApi.Application.Abstractions;
using jobAgentApi.Application.Abstractions.Messaging;
using jobAgentApi.Application.Repositories;
using jobAgentApi.Domain.Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace jobAgentApi.Application.Features.User.Queries.GetUserCv;

public sealed class GetUserCvQueryHandler : IQueryHandler<GetUserCvQuery, GetUserCvResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPdfService _pdfService;

    public GetUserCvQueryHandler(IUnitOfWork unitOfWork, IPdfService pdfService)
    {
        _unitOfWork = unitOfWork;
        _pdfService = pdfService;
    }

    public async Task<GetUserCvResponse> Handle(GetUserCvQuery request, CancellationToken cancellationToken)
    {
        var userCvRepository = _unitOfWork.GetRepository<UserCv>();
        var allCvs = await userCvRepository.GetAllAsync();
        var userCv = allCvs.FirstOrDefault(cv => cv.UserId == request.UserId);

        if (userCv is null || string.IsNullOrEmpty(userCv.ExtractedText))
        {
            return new GetUserCvResponse
            {
                HasCv = false
            };
        }

        var pdfBytes = await _pdfService.GeneratePdfAsync(userCv.ExtractedText, cancellationToken);
        var fileName = ResolveFileName(userCv.UrlFile);

        return new GetUserCvResponse
        {
            HasCv = true,
            UserCvId = userCv.Id,
            UrlFile = userCv.UrlFile,
            PdfBytes = pdfBytes,
            FileName = fileName,
            UploadedAtUtc = userCv.LastModifiedAt,
            FileSizeBytes = pdfBytes.LongLength
        };
    }

    private static string ResolveFileName(string? urlFile)
    {
        if (string.IsNullOrWhiteSpace(urlFile))
        {
            return "curriculo.pdf";
        }

        var lastSegment = urlFile
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault();

        if (string.IsNullOrWhiteSpace(lastSegment))
        {
            return "curriculo.pdf";
        }

        var cleanSegment = lastSegment.Split('?', '#')[0];
        var separatorIndex = cleanSegment.IndexOf('-');

        if (separatorIndex >= 0 && separatorIndex < cleanSegment.Length - 1)
        {
            return cleanSegment[(separatorIndex + 1)..];
        }

        return cleanSegment;
    }
}
