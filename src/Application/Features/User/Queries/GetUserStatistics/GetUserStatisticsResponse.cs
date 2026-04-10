namespace jobAgentApi.Application.Features.User.Queries.GetUserStatistics;

public sealed record UserStatisticsResponse(
    Overview Overview,
    ApplicationsByDay ApplicationsByDay,
    StatusDistribution StatusDistribution,
    PlatformDistribution PlatformDistribution
);

public sealed record Overview(
    int Total,
    int TotalPercentageChange, // +12% vs mês passado
    int Applied,
    double AppliedSuccessRate, // 8.2% de sucesso
    int Skipped,
    int Failed,
    int FailedWeeklyChange // -2% essa semana
);

public sealed record ApplicationsByDay(
    List<ApplicationDayCount> Data
);

public sealed record ApplicationDayCount(
    DateTime Date,
    int Count
);

public sealed record StatusDistribution(
    int Total,
    int Applied,
    double AppliedPercentage,
    int Skipped,
    double SkippedPercentage,
    int Failed,
    double FailedPercentage
);

public sealed record PlatformCount(
    string Platform,
    int Count
);

public sealed record PlatformDistribution(
    List<PlatformCount> Data
);
