using jobAgentApi.Application.Abstractions.Messaging;

namespace jobAgentApi.Application.Features.Jobs.Commands.RunJobScraper;

public sealed record RunJobScraperCommand(
    Guid? UserId,
    Guid? SearchQueryId) : ICommand<RunJobScraperResponse>;