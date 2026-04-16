using jobAgentApi.Domain.Entities;

namespace jobAgentApi.Application.Repositories;

public record UserSearchQueryDto(Guid SearchQueryId, string Query);

public interface IUserSearchQueryRepository
{
    Task<bool> HasUserQueriesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<UserSearchQueryDto>> GetUserSearchQueriesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Guid?> GetUserCurrentSearchQueryIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<SearchQuery?> GetUserCurrentSearchQueryAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<int> GetUsersCountBySearchQueryAsync(Guid searchQueryId, CancellationToken cancellationToken = default);
    Task RemoveUserFromSearchQueryAsync(Guid userId, Guid searchQueryId, CancellationToken cancellationToken = default);
    Task DeleteOrphanSearchQueryAsync(Guid searchQueryId, CancellationToken cancellationToken = default);
    Task UpdateSearchQueryLastExecutedAsync(Guid searchQueryId, CancellationToken cancellationToken = default);
}
