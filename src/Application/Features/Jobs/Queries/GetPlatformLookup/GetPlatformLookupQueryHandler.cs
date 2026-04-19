using jobAgentApi.Application.Abstractions.Messaging;
using jobAgentApi.Application.Repositories;

namespace jobAgentApi.Application.Features.Jobs.Queries.GetPlatformLookup;

public sealed class GetPlatformLookupQueryHandler : IQueryHandler<GetPlatformLookupQuery, PlatformLookupResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetPlatformLookupQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PlatformLookupResponse> Handle(GetPlatformLookupQuery request, CancellationToken cancellationToken)
    {
        var safeLimit = Math.Clamp(request.Limit, 1, 100);

        var platforms = await _unitOfWork
            .GetJobRepository()
            .GetPlatformLookupAsync(request.UserId, request.Search, safeLimit, cancellationToken);

        return new PlatformLookupResponse(platforms);
    }
}
