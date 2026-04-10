using jobAgentApi.Application.Abstractions.Messaging;
using jobAgentApi.Application.Repositories;
using jobAgentApi.Domain.Entities;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace jobAgentApi.Application.Features.User.Queries.GetGeneratedCvs;

public sealed class GetGeneratedCvsQueryHandler : IQueryHandler<GetGeneratedCvsQuery, GetGeneratedCvsResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetGeneratedCvsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<GetGeneratedCvsResponse> Handle(GetGeneratedCvsQuery request, CancellationToken cancellationToken)
    {
        var generatedCvRepository = _unitOfWork.GetRepository<GeneratedCv>();
        var allGeneratedCvs = await generatedCvRepository.GetAllAsync();
        
        var userGeneratedCvs = allGeneratedCvs
            .Where(cv => cv.UserId == request.UserId && cv.Active)
            .OrderByDescending(cv => cv.CreatedAt)
            .ToList();

        var response = new GetGeneratedCvsResponse
        {
            Items = userGeneratedCvs.Select(cv => new GeneratedCvItemDto
            {
                Id = cv.Id,
                UrlFile = cv.UrlFile,
                CreatedAt = cv.CreatedAt,
                FileName = $"curriculo-gerado-{cv.CreatedAt:dd-MM-yyyy-HHmm}.pdf"
            }).ToList(),
            Total = userGeneratedCvs.Count
        };

        return response;
    }
}
