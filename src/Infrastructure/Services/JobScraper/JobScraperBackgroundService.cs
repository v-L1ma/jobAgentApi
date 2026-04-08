using jobAgentApi.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace jobAgentApi.Infrastructure.Services.JobScraper;

internal sealed class JobScraperBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<JobScraperOptions> _optionsMonitor;
    private readonly ILogger<JobScraperBackgroundService> _logger;

    public JobScraperBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<JobScraperOptions> optionsMonitor,
        ILogger<JobScraperBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _optionsMonitor.CurrentValue;
            var delayMinutes = Math.Max(1, options.IntervalMinutes);

            if (!options.Enabled)
            {
                _logger.LogInformation("Job scraper background execution is disabled via JobScraper:Enabled=false");
                await Task.Delay(TimeSpan.FromMinutes(delayMinutes), stoppingToken);
                continue;
            }

            try
            {
                // Cria um scope por execução para resolver serviços Scoped
                using var scope = _scopeFactory.CreateScope();
                var executionService = scope.ServiceProvider.GetRequiredService<IJobScraperExecutionService>();
                
                var report = await executionService.ExecuteAsync(
                    new JobScraperExecutionRequest("scheduled"),
                    stoppingToken);

                if (report is null)
                {
                    _logger.LogInformation("Scheduled run skipped because another execution is already running");
                }
                else
                {
                    _logger.LogInformation(
                        "Scheduled run completed. Queries={QueriesProcessed} Found={JobsFound} Saved={JobsSaved} Skipped={JobsSkipped} Errors={ErrorsCount}",
                        report.QueriesProcessed,
                        report.JobsFound,
                        report.JobsSaved,
                        report.JobsSkipped,
                        report.Errors.Count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in scheduled scraper execution loop");
            }

            await Task.Delay(TimeSpan.FromMinutes(delayMinutes), stoppingToken);
        }
    }
}