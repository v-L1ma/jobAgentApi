namespace jobAgentApi.Application.Repositories
{
    public interface IUnitOfWork
    {
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        Task BeginTransactionAsync(CancellationToken cancellationToken = default);
        Task CommitAsync(CancellationToken cancellationToken = default);
        Task RollbackAsync(CancellationToken cancellationToken = default);
        IUserRepository GetUserRepository();
        IJobRepository GetJobRepository();
        IRepositoryBase<T> GetRepository<T>() where T : class;
    }
}
