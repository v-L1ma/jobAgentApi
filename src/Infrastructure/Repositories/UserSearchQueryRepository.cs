using jobAgentApi.Application.Repositories;
using Microsoft.EntityFrameworkCore;

namespace jobAgentApi.Infrastructure.Repositories;

internal sealed class UserSearchQueryRepository : IUserSearchQueryRepository
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
}
