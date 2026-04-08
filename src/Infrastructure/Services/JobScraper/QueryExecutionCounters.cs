using System.Collections.Concurrent;
using jobAgentApi.Domain.Enums;

namespace jobAgentApi.Infrastructure.Services.JobScraper;

internal sealed class QueryExecutionCounters
{
    private readonly ConcurrentDictionary<(Guid QueryId, Platform Platform), int> _jobsAddedPerQueryPlatform = new();
    
    public int TotalJobsFound { get; set; }
    public int TotalJobsSaved { get; set; }
    public int TotalJobsSkipped { get; set; }
    public int TotalQueriesProcessed { get; set; }

    public int GetJobsAddedCount(Guid queryId, Platform platform)
    {
        return _jobsAddedPerQueryPlatform.GetValueOrDefault((queryId, platform), 0);
    }

    public bool TryAddJob(Guid queryId, Platform platform, int limit)
    {
        var current = _jobsAddedPerQueryPlatform.GetOrAdd((queryId, platform), _ => 0);
        
        if (current >= limit)
        {
            return false;
        }

        _jobsAddedPerQueryPlatform[(queryId, platform)] = current + 1;
        return true;
    }

    public void MarkSkipped()
    {
        TotalJobsSkipped++;
    }

    public void MarkFound()
    {
        TotalJobsFound++;
    }

    public void MarkSaved()
    {
        TotalJobsSaved++;
    }
}
