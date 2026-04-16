using jobAgentApi.Application.Repositories;
using jobAgentApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace jobAgentApi.Infrastructure.Repositories;

public sealed class UserSearchQueryRepository : IUserSearchQueryRepository
{
    private readonly AppDbContext _dbContext;

    public UserSearchQueryRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> HasUserQueriesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.UserSearchQueries
            .AnyAsync(usq => usq.UserId == userId, cancellationToken);
    }

    public async Task<List<UserSearchQueryDto>> GetUserSearchQueriesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await (from usq in _dbContext.UserSearchQueries.AsNoTracking()
                      join sq in _dbContext.SearchQueries.AsNoTracking() on usq.SearchQueryId equals sq.Id
                      where usq.UserId == userId
                      select new UserSearchQueryDto(sq.Id, sq.Query))
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid?> GetUserCurrentSearchQueryIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.UserSearchQueries
            .AsNoTracking()
            .Where(usq => usq.UserId == userId)
            .Select(usq => (Guid?)usq.SearchQueryId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> GetUsersCountBySearchQueryAsync(Guid searchQueryId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.UserSearchQueries
            .AsNoTracking()
            .CountAsync(usq => usq.SearchQueryId == searchQueryId, cancellationToken);
    }

    public async Task RemoveUserFromSearchQueryAsync(Guid userId, Guid searchQueryId, CancellationToken cancellationToken = default)
    {
        var userSearchQuery = await _dbContext.UserSearchQueries
            .FirstOrDefaultAsync(usq => usq.UserId == userId && usq.SearchQueryId == searchQueryId, cancellationToken);

        if (userSearchQuery != null)
        {
            _dbContext.UserSearchQueries.Remove(userSearchQuery);
        }
    }

    public async Task DeleteOrphanSearchQueryAsync(Guid searchQueryId, CancellationToken cancellationToken = default)
    {
        var usersCount = await GetUsersCountBySearchQueryAsync(searchQueryId, cancellationToken);

        if (usersCount == 0)
        {
            var searchQuery = await _dbContext.SearchQueries
                .FirstOrDefaultAsync(sq => sq.Id == searchQueryId, cancellationToken);

            if (searchQuery != null)
            {
                _dbContext.SearchQueries.Remove(searchQuery);
            }
        }
    }

    public async Task<SearchQuery?> GetUserCurrentSearchQueryAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await (from usq in _dbContext.UserSearchQueries.AsNoTracking()
                      join sq in _dbContext.SearchQueries.AsNoTracking() on usq.SearchQueryId equals sq.Id
                      where usq.UserId == userId
                      select sq)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpdateSearchQueryLastExecutedAsync(Guid searchQueryId, CancellationToken cancellationToken = default)
    {
        var searchQuery = await _dbContext.SearchQueries
            .FirstOrDefaultAsync(sq => sq.Id == searchQueryId, cancellationToken);

        if (searchQuery != null)
        {
            searchQuery.LastExecutedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
