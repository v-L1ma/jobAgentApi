namespace jobAgentApi.Application.Repositories
{
    public interface IRepositoryBase<T> where T : class
    {
        Task<T?> GetByIdAsync(int id);
        Task<IEnumerable<T>> GetAllAsync();
        Task<bool> ExistsAsync(int id);
        Task<bool> AddAsync(T entity);
        Task<bool> UpdateAsync(T entity);
        Task<bool> DeleteAsync(T entity);
        Task SaveChangesAsync();
    }
}
