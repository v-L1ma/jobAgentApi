using jobAgentApi.Application.Abstractions;
using jobAgentApi.Application.Repositories;
using jobAgentApi.Domain.Entities;
using jobAgentApi.Infrastructure.Services.Cache;
using jobAgentApi.Infrastructure.Services.JobScraper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace jobAgentApi.Infrastructure.Services.JobScraperQueue;

internal sealed class JobScrapingQueueBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IJobScrapingQueueService _queueService;
    private readonly IJobCacheService _cacheService;
    private readonly IOptions<JobScraperOptions> _options;
    private readonly ILogger<JobScrapingQueueBackgroundService> _logger;

    public JobScrapingQueueBackgroundService(
        IServiceScopeFactory scopeFactory,
        IJobScrapingQueueService queueService,
        IJobCacheService cacheService,
        IOptions<JobScraperOptions> options,
        ILogger<JobScrapingQueueBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _queueService = queueService;
        _cacheService = cacheService;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Job Scraping Queue Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var request = await _queueService.TryDequeueAsync(stoppingToken);
                
                if (request == null)
                {
                    continue;
                }

                _logger.LogInformation(
                    "Processing scraping request '{RequestId}' for query '{Query}'",
                    request.RequestId,
                    request.NormalizedQuery);

                using var scope = _scopeFactory.CreateScope();
                var executionService = scope.ServiceProvider.GetRequiredService<IJobScraperExecutionService>();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Executa o scraping para esta query específica
                var report = await ExecuteScrapingForQueryAsync(
                    executionService,
                    dbContext,
                    request,
                    stoppingToken);

                if (report != null)
                {
                    // Busca as vagas coletadas e salva no cache
                    var jobs = await FetchJobsForQueryAsync(dbContext, stoppingToken);
                    
                    var cachedJobs = jobs.Select(j => new CachedJobItem(
                        j.Id,
                        j.Title,
                        j.Description,
                        j.Url,
                        j.IsApplied)).ToList();

                    var cacheResult = new CachedJobSearchResult(
                        cachedJobs,
                        DateTime.UtcNow,
                        IsComplete: true);

                    await _cacheService.SetCachedResultAsync(request.NormalizedQuery, cacheResult, stoppingToken);

                    _logger.LogInformation(
                        "Scraping request '{RequestId}' completed. Found {JobCount} jobs for query '{Query}'",
                        request.RequestId,
                        cachedJobs.Count,
                        request.NormalizedQuery);
                }

                _queueService.MarkQueryCompleted(request.NormalizedQuery);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing scraping queue");
            }
        }

        _logger.LogInformation("Job Scraping Queue Background Service stopped");
    }

    private async Task<JobScraperExecutionReport?> ExecuteScrapingForQueryAsync(
        IJobScraperExecutionService executionService,
        AppDbContext dbContext,
        ScrapingQueueRequest request,
        CancellationToken cancellationToken)
    {
        // Encontra o SearchQuery correspondente
        var searchQuery = await dbContext.SearchQueries
            .FirstOrDefaultAsync(sq => sq.Query.ToLower().Trim() == request.NormalizedQuery.ToLower().Trim(), cancellationToken);

        if (searchQuery == null)
        {
            _logger.LogWarning("SearchQuery not found for normalized query '{Query}'", request.NormalizedQuery);
            return null;
        }

        // Se temos UserId, executa com contexto do usuário
        if (request.UserId.HasValue)
        {
            return await executionService.ExecuteAsync(
                new JobScraperExecutionRequest("queue", request.UserId, searchQuery.Id),
                cancellationToken);
        }

        // Caso contrário, executa apenas para esta query
        return await executionService.ExecuteAsync(
            new JobScraperExecutionRequest("queue", SearchQueryId: searchQuery.Id),
            cancellationToken);
    }

    private async Task<List<Job>> FetchJobsForQueryAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // Busca todas as vagas (pode ser otimizado com filtros mais específicos)
        return await dbContext.Jobs
            .AsNoTracking()
            .OrderByDescending(j => j.CreatedAt)
            .Take(100) // Limite para MVP
            .ToListAsync(cancellationToken);
    }
}
