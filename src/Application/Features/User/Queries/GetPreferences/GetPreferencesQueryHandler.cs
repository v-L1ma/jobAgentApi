using jobAgentApi.Application.Abstractions.Messaging;
using jobAgentApi.Application.Repositories;
using System.Threading;
using System.Threading.Tasks;

namespace jobAgentApi.Application.Features.User.Queries.GetPreferences;

public sealed class GetPreferencesQueryHandler : IQueryHandler<GetPreferencesQuery, UserPreferencesDto>
{
    private readonly IUserSearchQueryRepository _userSearchQueryRepository;

    public GetPreferencesQueryHandler(IUserSearchQueryRepository userSearchQueryRepository)
    {
        _userSearchQueryRepository = userSearchQueryRepository;
    }

    public async Task<UserPreferencesDto> Handle(GetPreferencesQuery request, CancellationToken cancellationToken)
    {
        var userSearchQuery = await _userSearchQueryRepository.GetUserCurrentSearchQueryAsync(request.UserId, cancellationToken);

        if (userSearchQuery is null)
        {
            return new UserPreferencesDto
            {
                UserId = request.UserId
            };
        }

        return new UserPreferencesDto
        {
            UserId = request.UserId,
            Skills = userSearchQuery.Keywords,
            Levels = userSearchQuery.Levels,
            Area = userSearchQuery.Area
        };
    }
}
