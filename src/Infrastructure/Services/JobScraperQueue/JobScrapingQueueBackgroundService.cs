using jobAgentApi.Application.Abstractions;
using jobAgentApi.Application.Repositories;
using jobAgentApi.Infrastructure.Services.JobScraper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace jobAgentApi.Infrastructure.Services.JobScraperQueue;

internal sealed class JobScrapingQueueBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IJobScrapingQueueService _queueService;
    private readonly ILogger<JobScrapingQueueBackgroundService> _logger;

    public JobScrapingQueueBackgroundService(
        IServiceScopeFactory scopeFactory,
        IJobScrapingQueueService queueService,
        ILogger<JobScrapingQueueBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _queueService = queueService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Job Scraping Queue Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            ScrapingQueueRequest? currentRequest = null;

            try
            {
                currentRequest = await _queueService.TryDequeueAsync(stoppingToken);
                
                if (currentRequest == null)
                {
                    continue;
                }

                _logger.LogInformation(
                    "Processing scraping request '{RequestId}' for query '{Query}'",
                    currentRequest.RequestId,
                    currentRequest.NormalizedQuery);

                using var scope = _scopeFactory.CreateScope();
                var executionService = scope.ServiceProvider.GetRequiredService<IJobScraperExecutionService>();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Executa o scraping para esta query específica
                var report = await ExecuteScrapingForQueryAsync(
                    executionService,
                    dbContext,
                    currentRequest,
                    stoppingToken);

                if (report != null)
                {
                    _logger.LogInformation(
                        "Scraping request '{RequestId}' completed for query '{Query}'",
                        currentRequest.RequestId,
                        currentRequest.NormalizedQuery);
                }

                _queueService.MarkQueryCompleted(currentRequest.NormalizedQuery);
                currentRequest = null;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (currentRequest is not null)
                {
                    _queueService.MarkQueryFailed(currentRequest.NormalizedQuery, ex);
                }

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
        // Se temos SearchQueryId, usa diretamente
        if (request.SearchQueryId.HasValue)
        {
            _logger.LogInformation(
                "Executando scraping com SearchQueryId '{SearchQueryId}' para UserId '{UserId}'",
                request.SearchQueryId.Value,
                request.UserId);

            return await executionService.ExecuteAsync(
                new JobScraperExecutionRequest("queue", request.UserId, request.SearchQueryId.Value),
                cancellationToken);
        }

        // Fallback: busca pela NormalizedQuery (cenários legados)
        var searchQuery = await dbContext.SearchQueries
            .AsNoTracking()
            .FirstOrDefaultAsync(sq => sq.Query.ToLower().Trim() == request.NormalizedQuery.ToLower().Trim(), cancellationToken);

        if (searchQuery == null)
        {
            _logger.LogWarning("SearchQuery não encontrada para normalized query '{Query}'", request.NormalizedQuery);
            return null;
        }

        if (request.UserId.HasValue)
        {
            return await executionService.ExecuteAsync(
                new JobScraperExecutionRequest("queue", request.UserId, searchQuery.Id),
                cancellationToken);
        }

        return await executionService.ExecuteAsync(
            new JobScraperExecutionRequest("queue", SearchQueryId: searchQuery.Id),
            cancellationToken);
    }
}
