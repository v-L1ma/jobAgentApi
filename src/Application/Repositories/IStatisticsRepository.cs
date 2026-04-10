namespace jobAgentApi.Application.Repositories;

public interface IStatisticsRepository
{
    Task<UserStatisticsData> GetAllStatisticsAsync(Guid userId, int daysForChart, CancellationToken cancellationToken);
}

public sealed class UserStatisticsData
{
    public int Total { get; set; }
    public int TotalPreviousMonth { get; set; }
    public int Applied { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public int FailedThisWeek { get; set; }
    public int FailedLastWeek { get; set; }
    public List<(DateTime Date, int Count)> JobsByDay { get; set; } = new();
    public List<(string Platform, int Count)> JobsByPlatform { get; set; } = new();
}
