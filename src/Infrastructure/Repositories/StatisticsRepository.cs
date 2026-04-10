using jobAgentApi.Application.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace jobAgentApi.Infrastructure.Repositories;

internal sealed class StatisticsRepository : IStatisticsRepository
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<StatisticsRepository> _logger;

    public StatisticsRepository(AppDbContext dbContext, ILogger<StatisticsRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<UserStatisticsData> GetAllStatisticsAsync(Guid userId, int daysForChart, CancellationToken cancellationToken)
    {
        var overviewSql = @"
            WITH UserJobs AS (
                SELECT DISTINCT j.""Id"", j.""Title"", j.""Description"", j.""Url"", 
                       j.""IsApplied"", j.""Status"", j.""PlataformJobId"", j.""CreatedAt""
                FROM ""Jobs"" j
                INNER JOIN ""UserSearchQueries"" usq ON usq.""UserId"" = {0}
                INNER JOIN ""SearchQueries"" sq ON sq.""Id"" = usq.""SearchQueryId""
                WHERE EXISTS (
                    SELECT 1 FROM unnest(sq.""Keywords"") as kw
                    WHERE j.""Title"" ILIKE '%' || kw || '%' 
                       OR j.""Description"" ILIKE '%' || kw || '%'
                )
            )
            SELECT 
                COUNT(*)::int as ""Total"",
                COUNT(*) FILTER (WHERE ""CreatedAt"" >= DATE_TRUNC('month', CURRENT_DATE - INTERVAL '1 month') 
                                  AND ""CreatedAt"" < DATE_TRUNC('month', CURRENT_DATE))::int as ""TotalPreviousMonth"",
                COUNT(*) FILTER (WHERE ""IsApplied"" = true)::int as ""Applied"",
                COUNT(*) FILTER (WHERE ""Status"" = 'skipped')::int as ""Skipped"",
                COUNT(*) FILTER (WHERE ""Status"" = 'failed')::int as ""Failed"",
                COUNT(*) FILTER (WHERE ""Status"" = 'failed' 
                                  AND ""CreatedAt"" >= DATE_TRUNC('week', CURRENT_DATE))::int as ""FailedThisWeek"",
                COUNT(*) FILTER (WHERE ""Status"" = 'failed' 
                                  AND ""CreatedAt"" >= DATE_TRUNC('week', CURRENT_DATE - INTERVAL '1 week')
                                  AND ""CreatedAt"" < DATE_TRUNC('week', CURRENT_DATE))::int as ""FailedLastWeek""
            FROM UserJobs";

        var jobsByDaySql = @"
            WITH UserJobs AS (
                SELECT DISTINCT j.""Id"", j.""Title"", j.""Description"", j.""Url"", 
                       j.""IsApplied"", j.""Status"", j.""PlataformJobId"", j.""CreatedAt""
                FROM ""Jobs"" j
                INNER JOIN ""UserSearchQueries"" usq ON usq.""UserId"" = {0}
                INNER JOIN ""SearchQueries"" sq ON sq.""Id"" = usq.""SearchQueryId""
                WHERE EXISTS (
                    SELECT 1 FROM unnest(sq.""Keywords"") as kw
                    WHERE j.""Title"" ILIKE '%' || kw || '%' 
                       OR j.""Description"" ILIKE '%' || kw || '%'
                )
            )
            SELECT
                DATE(""CreatedAt"") as ""Date"",
                COUNT(*)::int as ""Count""
            FROM UserJobs
            WHERE ""CreatedAt"" >= CURRENT_DATE - ({1} || ' days')::INTERVAL
            GROUP BY DATE(""CreatedAt"")
            ORDER BY ""Date"" ASC";

        var jobsByPlatformSql = @"
            WITH UserJobs AS (
                SELECT DISTINCT j.""Id"", j.""Title"", j.""Description"", j.""Url"", 
                       j.""IsApplied"", j.""Status"", j.""PlataformJobId"", j.""CreatedAt""
                FROM ""Jobs"" j
                INNER JOIN ""UserSearchQueries"" usq ON usq.""UserId"" = {0}
                INNER JOIN ""SearchQueries"" sq ON sq.""Id"" = usq.""SearchQueryId""
                WHERE EXISTS (
                    SELECT 1 FROM unnest(sq.""Keywords"") as kw
                    WHERE j.""Title"" ILIKE '%' || kw || '%' 
                       OR j.""Description"" ILIKE '%' || kw || '%'
                )
            )
            SELECT
                CASE
                    WHEN ""PlataformJobId"" ILIKE '%linkedin%' THEN 'LinkedIn'
                    WHEN ""PlataformJobId"" ILIKE '%greenhouse%' THEN 'Greenhouse'
                    WHEN ""PlataformJobId"" ILIKE '%gupy%' THEN 'Gupy'
                    ELSE 'Other'
                END as ""Platform"",
                COUNT(*)::int as ""Count""
            FROM UserJobs
            GROUP BY 1
            ORDER BY ""Count"" DESC";

        try
        {
            var overview = await _dbContext.Database
                .SqlQueryRaw<OverviewStatsRow>(overviewSql, userId)
                .SingleOrDefaultAsync(cancellationToken);

            var jobsByDayRows = await _dbContext.Database
                .SqlQueryRaw<JobsByDayRow>(jobsByDaySql, userId, daysForChart)
                .ToListAsync(cancellationToken);

            var jobsByPlatformRows = await _dbContext.Database
                .SqlQueryRaw<JobsByPlatformRow>(jobsByPlatformSql, userId)
                .ToListAsync(cancellationToken);

            var statistics = new UserStatisticsData();

            if (overview is not null)
            {
                statistics.Total = overview.Total;
                statistics.TotalPreviousMonth = overview.TotalPreviousMonth;
                statistics.Applied = overview.Applied;
                statistics.Skipped = overview.Skipped;
                statistics.Failed = overview.Failed;
                statistics.FailedThisWeek = overview.FailedThisWeek;
                statistics.FailedLastWeek = overview.FailedLastWeek;
            }

            statistics.JobsByDay = jobsByDayRows
                .Select(row => (row.Date, row.Count))
                .ToList();

            statistics.JobsByPlatform = jobsByPlatformRows
                .Select(row => (row.Platform, row.Count))
                .ToList();

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar queries de estatísticas para o usuário {UserId}", userId);
            throw;
        }
    }

    private sealed class OverviewStatsRow
    {
        public int Total { get; init; }
        public int TotalPreviousMonth { get; init; }
        public int Applied { get; init; }
        public int Skipped { get; init; }
        public int Failed { get; init; }
        public int FailedThisWeek { get; init; }
        public int FailedLastWeek { get; init; }
    }

    private sealed class JobsByDayRow
    {
        public DateTime Date { get; init; }
        public int Count { get; init; }
    }

    private sealed class JobsByPlatformRow
    {
        public string Platform { get; init; } = string.Empty;
        public int Count { get; init; }
    }

    // Métodos legados mantidos para compatibilidade (não usar)
    public Task<int> CountTotalJobsAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(0);
    public Task<int> CountTotalJobsPreviousMonthAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(0);
    public Task<int> CountAppliedJobsAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(0);
    public Task<int> CountSkippedJobsAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(0);
    public Task<int> CountFailedJobsAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(0);
    public Task<int> CountFailedJobsThisWeekAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(0);
    public Task<int> CountFailedJobsLastWeekAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(0);
    public Task<List<(DateTime Date, int Count)>> GetJobsByDayAsync(Guid userId, int days, CancellationToken cancellationToken) => Task.FromResult(new List<(DateTime, int)>());
    public Task<List<(string Platform, int Count)>> GetJobsByPlatformAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(new List<(string, int)>());
}
