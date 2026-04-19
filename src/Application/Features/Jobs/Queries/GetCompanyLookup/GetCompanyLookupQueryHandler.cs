using jobAgentApi.Application.Abstractions.Messaging;
using jobAgentApi.Application.Repositories;

namespace jobAgentApi.Application.Features.Jobs.Queries.GetCompanyLookup;

public sealed class GetCompanyLookupQueryHandler : IQueryHandler<GetCompanyLookupQuery, CompanyLookupResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetCompanyLookupQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<CompanyLookupResponse> Handle(GetCompanyLookupQuery request, CancellationToken cancellationToken)
    {
        var safeLimit = Math.Clamp(request.Limit, 1, 100);

        var companies = await _unitOfWork
            .GetJobRepository()
            .GetCompanyLookupAsync(request.UserId, request.Search, safeLimit, cancellationToken);

        return new CompanyLookupResponse(companies);
    }
}
