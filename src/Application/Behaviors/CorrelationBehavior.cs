using MediatR;
using Microsoft.Extensions.Logging;

namespace jobAgentApi.Application.Behaviors;

public sealed class CorrelationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<CorrelationBehavior<TRequest, TResponse>> _logger;

    public CorrelationBehavior(
        ILogger<CorrelationBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid();

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            return await next();
        }
    }
}
