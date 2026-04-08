namespace jobAgentApi.Infrastructure.Services.JobScraper;

public sealed class JobScraperOptions
{
    public const string SectionName = "JobScraper";

    public bool Enabled { get; set; }
    public int IntervalMinutes { get; set; } = 30;
    public bool Headless { get; set; } = true;
    public int SlowMoMs { get; set; }
    public int NavigationTimeoutMs { get; set; } = 15000;
    public int MinDelayMs { get; set; } = 1000;
    public int MaxDelayMs { get; set; } = 2500;
    public int MaxJobsPerQuery { get; set; } = 20;
    public int MaxApplicationsPerDay { get; set; } = 50;
    public bool EasyApplyOnly { get; set; } = true;
    public int MaxScrollIterations { get; set; } = 20;
    public int MaxJobsPerExecution { get; set; } = 100;
    public int MaxLinkedInJobsPerQuery { get; set; } = 20;
    public int MaxGupyJobsPerQuery { get; set; } = 50;
    public int MaxGreenhouseJobsPerQuery { get; set; } = 50;
    public int RetryCount { get; set; } = 2;
    public int RetryBaseDelayMs { get; set; } = 1000;
    public string ScreenshotsPath { get; set; } = "logs/errors";
    public string LiAtCookie { get; set; } = string.Empty;
}