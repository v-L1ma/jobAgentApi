using System.Collections.Concurrent;
using System.Threading.Channels;
using jobAgentApi.Application.Abstractions;
using jobAgentApi.Infrastructure.Services.JobScraper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace jobAgentApi.Infrastructure.Services.JobScraperQueue;

public sealed class JobScrapingQueueService : IJobScrapingQueueService
{
    private readonly Channel<ScrapingQueueRequest> _channel;
    private readonly ConcurrentDictionary<string, ScrapingQueryState> _queriesInProgress;
    private readonly ILogger<JobScrapingQueueService> _logger;

    public JobScrapingQueueService(
        ILogger<JobScrapingQueueService> logger,
        IOptions<JobScraperOptions> scraperOptions)
    {
        var queueCapacity = Math.Max(10, scraperOptions.Value.QueueCapacity);

        _channel = Channel.CreateBounded<ScrapingQueueRequest>(new BoundedChannelOptions(queueCapacity)
        {
            SingleWriter = false,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        _queriesInProgress = new ConcurrentDictionary<string, ScrapingQueryState>(StringComparer.OrdinalIgnoreCase);
        _logger = logger;

        _logger.LogInformation("Job scraping queue initialized with capacity {QueueCapacity}", queueCapacity);
    }

    public async Task EnqueueScrapingRequestAsync(ScrapingQueueRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedQuery = request.NormalizedQuery;

        // Evita scraping duplicado para a mesma query
        if (_queriesInProgress.TryGetValue(normalizedQuery, out var existingState))
        {
            _logger.LogInformation(
                "Scraping already in progress for query '{Query}', returning existing request ID",
                normalizedQuery);
            
            request.SetRequestId(existingState.RequestId);
            return;
        }

        var requestId = Guid.NewGuid();
        var queryState = new ScrapingQueryState(requestId, DateTime.UtcNow);

        _queriesInProgress.TryAdd(normalizedQuery, queryState);
        request.SetRequestId(requestId);

        _logger.LogInformation(
            "Enqueuing scraping request '{RequestId}' for query '{Query}'",
            requestId,
            normalizedQuery);

        await _channel.Writer.WriteAsync(request, cancellationToken);
    }

    public Task<ScrapingQueueStatus> GetStatusAsync(string normalizedQuery, CancellationToken cancellationToken = default)
    {
        if (_queriesInProgress.TryGetValue(normalizedQuery, out var state))
        {
            return Task.FromResult(new ScrapingQueueStatus(
                RequestId: state.RequestId,
                IsRunning: true,
                StartedAtUtc: state.StartedAtUtc));
        }

        return Task.FromResult(new ScrapingQueueStatus(
            RequestId: null,
            IsRunning: false,
            StartedAtUtc: null));
    }

    public bool IsQueryInProgress(string normalizedQuery)
    {
        return _queriesInProgress.ContainsKey(normalizedQuery);
    }

    public async Task<ScrapingQueueRequest?> TryDequeueAsync(CancellationToken cancellationToken = default)
    {
        return await DequeueAsync(cancellationToken);
    }

    public void MarkQueryCompleted(string normalizedQuery)
    {
        if (_queriesInProgress.TryRemove(normalizedQuery, out var state))
        {
            _logger.LogInformation(
                "Scraping request '{RequestId}' completed for query '{Query}'",
                state.RequestId,
                normalizedQuery);
        }
    }

    public void MarkQueryFailed(string normalizedQuery, Exception error)
    {
        if (_queriesInProgress.TryRemove(normalizedQuery, out var state))
        {
            _logger.LogError(
                error,
                "Scraping request '{RequestId}' failed for query '{Query}'",
                state.RequestId,
                normalizedQuery);
        }
    }

    private async Task<ScrapingQueueRequest?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _channel.Reader.ReadAsync(cancellationToken);
        }
        catch (ChannelClosedException)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public IAsyncEnumerable<ScrapingQueueRequest> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}

sealed record ScrapingQueryState(Guid RequestId, DateTime StartedAtUtc);
