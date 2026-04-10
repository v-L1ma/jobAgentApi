using jobAgentApi.Application.Abstractions.Messaging;

namespace jobAgentApi.Application.Features.User.Queries.GetUserStatistics;

public sealed record GetUserStatisticsQuery(Guid UserId) : IQuery<UserStatisticsResponse>;
