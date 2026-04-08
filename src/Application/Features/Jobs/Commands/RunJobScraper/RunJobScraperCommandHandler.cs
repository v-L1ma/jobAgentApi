using jobAgentApi.Application.Abstractions;
using jobAgentApi.Application.Abstractions.Messaging;

namespace jobAgentApi.Application.Features.Jobs.Commands.RunJobScraper;

public sealed class RunJobScraperCommandHandler : ICommandHandler<RunJobScraperCommand, RunJobScraperResponse>
{
    private readonly IJobScraperExecutionService _jobScraperExecutionService;

    public RunJobScraperCommandHandler(IJobScraperExecutionService jobScraperExecutionService)
    {
        _jobScraperExecutionService = jobScraperExecutionService;
    }

    public async Task<RunJobScraperResponse> Handle(RunJobScraperCommand request, CancellationToken cancellationToken)
    {
        var report = await _jobScraperExecutionService.ExecuteAsync(
            new JobScraperExecutionRequest("manual", request.UserId, request.SearchQueryId),
            cancellationToken);

        if (report is null)
        {
            throw new DomainException("Uma execução do scraper já está em andamento.", 409);
        }

        return new RunJobScraperResponse(
            report.ExecutionId,
            report.StartedAtUtc,
            report.FinishedAtUtc,
            report.QueriesProcessed,
            report.JobsFound,
            report.JobsSaved,
            report.JobsSkipped,
            report.Errors);
    }
}
