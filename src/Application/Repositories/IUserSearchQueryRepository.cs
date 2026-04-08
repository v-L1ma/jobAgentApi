namespace jobAgentApi.Application.Repositories;

public interface IUserSearchQueryRepository
{
    Task<bool> HasUserQueriesAsync(Guid userId, CancellationToken cancellationToken = default);
}
