namespace jobAgentApi.Application.Abstractions;

public sealed class ScrapingQueueRequest
{
    public string NormalizedQuery { get; }
    public Guid? UserId { get; }
    public Guid? SearchQueryId { get; }
    public Guid RequestId { get; private set; }

    public ScrapingQueueRequest(string normalizedQuery, Guid? userId = null, Guid? searchQueryId = null)
    {
        NormalizedQuery = normalizedQuery;
        UserId = userId;
        SearchQueryId = searchQueryId;
        RequestId = Guid.Empty;
    }

    public void SetRequestId(Guid requestId)
    {
        RequestId = requestId;
    }
}
