namespace jobAgentApi.Application.Abstractions;

public sealed class ScrapingQueueRequest
{
    public string NormalizedQuery { get; }
    public Guid? UserId { get; }
    public Guid RequestId { get; private set; }

    public ScrapingQueueRequest(string normalizedQuery, Guid? userId = null)
    {
        NormalizedQuery = normalizedQuery;
        UserId = userId;
        RequestId = Guid.Empty;
    }

    public void SetRequestId(Guid requestId)
    {
        RequestId = requestId;
    }
}
