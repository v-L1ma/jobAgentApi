using jobAgentApi.Application.Abstractions;
using jobAgentApi.Application.Abstractions.Messaging;
using jobAgentApi.Application.Repositories;

namespace jobAgentApi.Application.Features.Jobs.Queries.GetJobs;

public sealed class GetJobsQueryHandler : IQueryHandler<GetJobsQuery, PagedJobsResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJobScraperExecutionService _jobScraperExecutionService;
    private readonly IUserSearchQueryRepository _userSearchQueryRepository;

    public GetJobsQueryHandler(
        IUnitOfWork unitOfWork,
        IJobScraperExecutionService jobScraperExecutionService,
        IUserSearchQueryRepository userSearchQueryRepository)
    {
        _unitOfWork = unitOfWork;
        _jobScraperExecutionService = jobScraperExecutionService;
        _userSearchQueryRepository = userSearchQueryRepository;
    }

    public async Task<PagedJobsResponse> Handle(GetJobsQuery request, CancellationToken cancellationToken)
    {
        ScraperTriggerResult? scraperResult = null;

        if (request.TriggerScraper && request.UserId.HasValue && request.UserId.Value != Guid.Empty)
        {
            scraperResult = await TriggerScraperAsync(request.UserId.Value, cancellationToken);
        }

        var jobRepository = _unitOfWork.GetJobRepository();

        var combinedQuery = string.Join(
            " ",
            new[] { request.Stack, request.Location }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));

        var query = string.IsNullOrWhiteSpace(combinedQuery) ? null : combinedQuery;

        var (items, totalCount) = await jobRepository.GetPagedAsync(
            query,
            request.UserId,
            request.Page,
            request.PageSize,
            cancellationToken);

        var totalPages = totalCount > 0 ? (int)Math.Ceiling(totalCount / (double)request.PageSize) : 0;

        var responseItems = items.Select(j => new JobListItemResponse(j.Id, j.Title, j.Description, j.Url, j.IsApplied)).ToList();

        return new PagedJobsResponse(responseItems, totalCount, request.Page, totalPages, scraperResult);
    }

    private async Task<ScraperTriggerResult?> TriggerScraperAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (_jobScraperExecutionService.IsRunning)
        {
            return new ScraperTriggerResult(false, "Scraper já está em execução.");
        }

        var hasUserQueries = await _userSearchQueryRepository.HasUserQueriesAsync(userId, cancellationToken);

        if (!hasUserQueries)
        {
            return new ScraperTriggerResult(false, "Nenhuma search query configurada. Configure suas preferências em /api/users/preferences.");
        }

        var executionReport = await _jobScraperExecutionService.ExecuteAsync(
            new JobScraperExecutionRequest("get_jobs_trigger", userId),
            cancellationToken);

        if (executionReport is null)
        {
            return new ScraperTriggerResult(false, "Falha ao iniciar scraper. Tente novamente em instantes.");
        }

        return new ScraperTriggerResult(
            true,
            $"Scraper executado. {executionReport.JobsSaved} novas vagas salvas de {executionReport.JobsFound} encontradas.",
            executionReport.ExecutionId);
    }
}
