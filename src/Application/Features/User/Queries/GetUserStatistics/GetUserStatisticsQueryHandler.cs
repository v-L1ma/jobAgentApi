using jobAgentApi.Application.Abstractions.Messaging;
using jobAgentApi.Application.Repositories;

namespace jobAgentApi.Application.Features.User.Queries.GetUserStatistics;

public sealed class GetUserStatisticsQueryHandler : IQueryHandler<GetUserStatisticsQuery, UserStatisticsResponse>
{
    private readonly IStatisticsRepository _statisticsRepository;

    public GetUserStatisticsQueryHandler(IStatisticsRepository statisticsRepository)
    {
        _statisticsRepository = statisticsRepository;
    }

    public async Task<UserStatisticsResponse> Handle(GetUserStatisticsQuery request, CancellationToken cancellationToken)
    {
        // Busca todas as estatísticas em uma única query otimizada
        var statistics = await _statisticsRepository.GetAllStatisticsAsync(request.UserId, 30, cancellationToken);

        // Calcula porcentagens
        var totalPercentageChange = statistics.TotalPreviousMonth > 0
            ? (int)Math.Round(((double)(statistics.Total - statistics.TotalPreviousMonth) / statistics.TotalPreviousMonth) * 100)
            : 0;

        var appliedSuccessRate = statistics.Total > 0
            ? Math.Round((double)statistics.Applied / statistics.Total * 100, 1)
            : 0;

        var failedWeeklyChange = statistics.FailedLastWeek > 0
            ? (int)Math.Round(((double)(statistics.FailedThisWeek - statistics.FailedLastWeek) / statistics.FailedLastWeek) * 100)
            : 0;

        // Distribuição por status
        var appliedPercentage = statistics.Total > 0 ? Math.Round((double)statistics.Applied / statistics.Total * 100, 0) : 0;
        var skippedPercentage = statistics.Total > 0 ? Math.Round((double)statistics.Skipped / statistics.Total * 100, 0) : 0;
        var failedPercentage = statistics.Total > 0 ? Math.Round((double)statistics.Failed / statistics.Total * 100, 0) : 0;

        // Candidaturas por dia
        var applicationsByDay = new ApplicationsByDay(
            statistics.JobsByDay.Select(x => new ApplicationDayCount(x.Date, x.Count)).ToList()
        );

        // Distribuição por plataforma
        var platformDistribution = new PlatformDistribution(
            statistics.JobsByPlatform.Select(x => new PlatformCount(x.Platform, x.Count)).ToList()
        );

        return new UserStatisticsResponse(
            Overview: new Overview(
                Total: statistics.Total,
                TotalPercentageChange: totalPercentageChange,
                Applied: statistics.Applied,
                AppliedSuccessRate: appliedSuccessRate,
                Skipped: statistics.Skipped,
                Failed: statistics.Failed,
                FailedWeeklyChange: failedWeeklyChange
            ),
            ApplicationsByDay: applicationsByDay,
            StatusDistribution: new StatusDistribution(
                Total: statistics.Total,
                Applied: statistics.Applied,
                AppliedPercentage: appliedPercentage,
                Skipped: statistics.Skipped,
                SkippedPercentage: skippedPercentage,
                Failed: statistics.Failed,
                FailedPercentage: failedPercentage
            ),
            PlatformDistribution: platformDistribution
        );
    }
}
