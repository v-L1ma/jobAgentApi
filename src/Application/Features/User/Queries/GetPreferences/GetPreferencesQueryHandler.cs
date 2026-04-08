using jobAgentApi.Application.Abstractions;
using jobAgentApi.Application.Abstractions.Messaging;
using jobAgentApi.Application.Repositories;
using jobAgentApi.Domain.Entities;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace jobAgentApi.Application.Features.User.Queries.GetPreferences;

public sealed class GetPreferencesQueryHandler : IQueryHandler<GetPreferencesQuery, UserPreferencesDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetPreferencesQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<UserPreferencesDto> Handle(GetPreferencesQuery request, CancellationToken cancellationToken)
    {
        var preferencesRepository = _unitOfWork.GetRepository<UserPreferences>();
        var allPreferences = await preferencesRepository.GetAllAsync();
        var userPreferences = allPreferences.FirstOrDefault(p => p.UserId == request.UserId);

        if (userPreferences is null)
        {
            return new UserPreferencesDto
            {
                UserId = request.UserId
            };
        }

        return new UserPreferencesDto
        {
            UserId = userPreferences.UserId,
            Skills = userPreferences.Skills,
            Level = userPreferences.Level,
            Area = userPreferences.Area
        };
    }
}
