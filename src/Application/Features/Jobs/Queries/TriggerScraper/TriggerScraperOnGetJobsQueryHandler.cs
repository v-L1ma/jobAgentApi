using jobAgentApi.Application.Abstractions;
using jobAgentApi.Application.Abstractions.Messaging;
using jobAgentApi.Application.Repositories;

namespace jobAgentApi.Application.Features.Jobs.Queries.TriggerScraper;

public sealed class TriggerScraperOnGetJobsQueryHandler : IQueryHandler<TriggerScraperOnGetJobsQuery, TriggerScraperResult>
{
    private readonly IJobScraperExecutionService _jobScraperExecutionService;
    private readonly IUserSearchQueryRepository _userSearchQueryRepository;

    public TriggerScraperOnGetJobsQueryHandler(
        IJobScraperExecutionService jobScraperExecutionService,
        IUserSearchQueryRepository userSearchQueryRepository)
    {
        _jobScraperExecutionService = jobScraperExecutionService;
        _userSearchQueryRepository = userSearchQueryRepository;
    }

    public async Task<TriggerScraperResult> Handle(TriggerScraperOnGetJobsQuery request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
        {
            return new TriggerScraperResult(false, "Usuário não autenticado.");
        }

        if (_jobScraperExecutionService.IsRunning)
        {
            return new TriggerScraperResult(false, "Scraper já está em execução.");
        }

        var hasUserQueries = await _userSearchQueryRepository.HasUserQueriesAsync(request.UserId, cancellationToken);

        if (!hasUserQueries)
        {
            return new TriggerScraperResult(false, "Nenhuma search query configurada para o usuário. Configure suas preferências primeiro.");
        }

        var executionReport = await _jobScraperExecutionService.ExecuteAsync(
            new JobScraperExecutionRequest("get_jobs_trigger", request.UserId),
            cancellationToken);

        if (executionReport is null)
        {
            return new TriggerScraperResult(false, "Falha ao iniciar execução do scraper. Tente novamente em instantes.");
        }

        return new TriggerScraperResult(
            true,
            $"Scraper disparado com sucesso. {executionReport.JobsSaved} novas vagas encontradas.",
            executionReport.ExecutionId);
    }
}
