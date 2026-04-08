using jobAgentApi.Application.Abstractions;
using jobAgentApi.Application.Abstractions.Messaging;
using jobAgentApi.Application.Repositories;

namespace jobAgentApi.Application.Features.Jobs.Queries.GetJobsAsync;

public sealed class GetJobsAsyncQueryHandler : IQueryHandler<GetJobsAsyncQuery, JobSearchResponse>
{
    private readonly IJobCacheService _cacheService;
    private readonly IJobScrapingQueueService _queueService;
    private readonly IUnitOfWork _unitOfWork;

    public GetJobsAsyncQueryHandler(
        IJobCacheService cacheService,
        IJobScrapingQueueService queueService,
        IUnitOfWork unitOfWork)
    {
        _cacheService = cacheService;
        _queueService = queueService;
        _unitOfWork = unitOfWork;
    }

    public async Task<JobSearchResponse> Handle(GetJobsAsyncQuery request, CancellationToken cancellationToken)
    {
        var normalizedQuery = NormalizeQuery(request.Query);

        // 1. Verifica se existe cache
        if (!string.IsNullOrEmpty(normalizedQuery))
        {
            var cachedResult = await _cacheService.GetCachedResultAsync(normalizedQuery, cancellationToken);
            
            if (cachedResult != null)
            {
                // Cache HIT - retorna imediatamente
                var items = cachedResult.Jobs.Select(j => new JobItemResponse(
                    j.Id,
                    j.Title,
                    j.Description,
                    j.Url,
                    j.IsApplied,
                    j.Company,
                    j.Location,
                    j.Platform)).ToList();

                return new JobSearchResponse(
                    Status: "complete",
                    IsLoading: false,
                    Data: items,
                    Meta: new JobSearchMeta(
                        ScraperRunning: false,
                        FromCache: true,
                        TotalItems: items.Count,
                        CurrentPage: request.Page,
                        TotalPages: 1));
            }
        }

        // 2. Cache MISS - verifica se já está em execução
        if (!string.IsNullOrEmpty(normalizedQuery) && _queueService.IsQueryInProgress(normalizedQuery))
        {
            var status = await _queueService.GetStatusAsync(normalizedQuery, cancellationToken);
            
            return new JobSearchResponse(
                Status: "partial",
                IsLoading: true,
                Data: Array.Empty<JobItemResponse>().ToList(),
                Meta: new JobSearchMeta(
                    ScraperRunning: true,
                    FromCache: false,
                    RequestId: status.RequestId));
        }

        // 3. Não está em execução - enfileira scraping e retorna parcial
        if (!string.IsNullOrEmpty(normalizedQuery) && request.UserId.HasValue)
        {
            var scrapingRequest = new ScrapingQueueRequest(normalizedQuery, request.UserId);
            await _queueService.EnqueueScrapingRequestAsync(scrapingRequest, cancellationToken);

            return new JobSearchResponse(
                Status: "partial",
                IsLoading: true,
                Data: Array.Empty<JobItemResponse>().ToList(),
                Meta: new JobSearchMeta(
                    ScraperRunning: true,
                    FromCache: false,
                    RequestId: scrapingRequest.RequestId));
        }

        // 4. Sem query - busca do banco normalmente
        var jobRepository = _unitOfWork.GetJobRepository();
        var (jobs, totalCount) = await jobRepository.GetPagedAsync(
            request.Stack,
            request.Location,
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
            j.IsApplied)).ToList();

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
