using jobAgentApi.Application.Abstractions;
using jobAgentApi.Application.Abstractions.Messaging;
using jobAgentApi.Application.Repositories;
using jobAgentApi.Domain.Entities;

namespace jobAgentApi.Application.Features.Jobs.Queries.GetJobsAsync;

public sealed class GetJobsAsyncQueryHandler : IQueryHandler<GetJobsAsyncQuery, JobSearchResponse>
{
    private readonly IJobScrapingQueueService _queueService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserSearchQueryRepository _userSearchQueryRepository;

    public GetJobsAsyncQueryHandler(
        IJobScrapingQueueService queueService,
        IUnitOfWork unitOfWork,
        IUserSearchQueryRepository userSearchQueryRepository)
    {
        _queueService = queueService;
        _unitOfWork = unitOfWork;
        _userSearchQueryRepository = userSearchQueryRepository;
    }

    public async Task<JobSearchResponse> Handle(GetJobsAsyncQuery request, CancellationToken cancellationToken)
    {
        Guid? searchQueryId = null;
        string? queryForScraping = null;
        SearchQuery? userSearchQuery = null;

        if (request.UserId.HasValue)
        {
            userSearchQuery = await _userSearchQueryRepository.GetUserCurrentSearchQueryAsync(request.UserId.Value, cancellationToken);

            if (userSearchQuery is not null)
            {
                searchQueryId = userSearchQuery.Id;
                queryForScraping = userSearchQuery.Query;
            }
        }

        var normalizedQuery = !string.IsNullOrEmpty(queryForScraping)
            ? NormalizeQuery(queryForScraping)
            : NormalizeQuery(request.Query);

        // caso seja uma página além da primeira, busca diretamente do banco sem considerar scraping
        if (request.Page > 1)
        {
            var jobRepositoryPaged = _unitOfWork.GetJobRepository();
            var (jobsPaged, totalCountPaged) = await jobRepositoryPaged.GetPagedAsync(
                request.Query,
                request.UserId,
                request.Page,
                request.PageSize,
                cancellationToken);

            var totalPagesPaged = totalCountPaged > 0 ? (int)Math.Ceiling(totalCountPaged / (double)request.PageSize) : 0;
            var jobItemsPaged = jobsPaged.Select(j => new JobItemResponse(
                j.Id,
                j.Title,
                j.Description,
                j.Url,
                j.IsApplied,
                Platform: j.Platform)).ToList();

            return new JobSearchResponse(
                Status: "complete",
                IsLoading: false,
                Data: jobItemsPaged,
                Meta: new JobSearchMeta(
                    ScraperRunning: false,
                    FromCache: false,
                    TotalItems: totalCountPaged,
                    CurrentPage: request.Page,
                    TotalPages: totalPagesPaged));
        }

        // Scraping só deve ser ativado para a primeira página (page == 1)
        // Para páginas seguintes, busca-se diretamente do banco (ver bloco acima)

        // 1. Verifica se a última execução foi há menos de 15 minutos
        var canRunScraper = true;
        if (userSearchQuery != null)
        {
            var hasPreviousExecution = userSearchQuery.LastExecutedAt > DateTime.MinValue;
            var timeSinceLastExecution = DateTime.UtcNow - userSearchQuery.LastExecutedAt;
            if (hasPreviousExecution && timeSinceLastExecution < TimeSpan.FromMinutes(15))
            {
                // Menos de 15 minutos - NÃO ativa o scraper, apenas busca do banco
                canRunScraper = false;
            }
        }

        // 2. Se pode rodar scraper, verifica se já está em execução ou enfileira
        var scraperRunning = false;
        Guid? requestId = null;

        if (canRunScraper && !string.IsNullOrEmpty(normalizedQuery) && request.UserId.HasValue)
        {
            // Verifica se já está em execução
            if (_queueService.IsQueryInProgress(normalizedQuery))
            {
                var status = await _queueService.GetStatusAsync(normalizedQuery, cancellationToken);
                scraperRunning = true;
                requestId = status.RequestId;
            }
            else
            {
                // Enfileira scraping
                var scrapingRequest = new ScrapingQueueRequest(normalizedQuery, request.UserId, searchQueryId);
                await _queueService.EnqueueScrapingRequestAsync(scrapingRequest, cancellationToken);
                scraperRunning = true;
                requestId = scrapingRequest.RequestId;

                // Atualiza o LastExecutedAt
                if (searchQueryId.HasValue)
                {
                    await _userSearchQueryRepository.UpdateSearchQueryLastExecutedAsync(searchQueryId.Value, cancellationToken);
                }
            }
        }

        // 3. Busca vagas do banco de dados
        var jobRepository = _unitOfWork.GetJobRepository();
        var (jobs, totalCount) = await jobRepository.GetPagedAsync(
            request.Query,
            request.UserId,
            request.Page,
            request.PageSize,
            cancellationToken);

        var totalPages = totalCount > 0 ? (int)Math.Ceiling(totalCount / (double)request.PageSize) : 0;
        var jobItems = jobs.Select(j => new JobItemResponse(
            j.Id,
            j.Title,
            j.Description,
            j.Url,
            j.IsApplied,
            Platform: j.Platform)).ToList();

        // Se o scraper está rodando, retorna como "partial" para indicar que mais dados virão
        if (scraperRunning)
        {
            return new JobSearchResponse(
                Status: "partial",
                IsLoading: true,
                Data: jobItems,
                Meta: new JobSearchMeta(
                    ScraperRunning: true,
                    FromCache: false,
                    TotalItems: totalCount,
                    CurrentPage: request.Page,
                    TotalPages: totalPages,
                    RequestId: requestId));
        }

        // Scraper não está rodando - retorna completo com dados do banco
        return new JobSearchResponse(
            Status: "complete",
            IsLoading: false,
            Data: jobItems,
            Meta: new JobSearchMeta(
                ScraperRunning: false,
                FromCache: false,
                TotalItems: totalCount,
                CurrentPage: request.Page,
                TotalPages: totalPages));
    }

    private static string? NormalizeQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        return query.Trim().ToLowerInvariant();
    }
}
