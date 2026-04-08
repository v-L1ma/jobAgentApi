using jobAgentApi.Domain.Entities;

namespace jobAgentApi.Application.Repositories;

public interface IJobRepository
{
    Task<Job?> GetByIdAsync(Guid id);
    Task<Job?> GetByPlataformJobIdOrUrlAsync(string plataformJobId, string url, CancellationToken cancellationToken = default);
    Task<int> CountJobsCreatedTodayAsync(DateTime dayStart, DateTime nextDay, CancellationToken cancellationToken = default);
    Task<(List<Job> Items, int TotalCount)> GetPagedAsync(
        string? stack,
        string? location,
        Guid? userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<bool> AddAsync(Job job, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Job job, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
