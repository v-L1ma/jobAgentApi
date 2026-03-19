using MediatR;

namespace jobAgentApi.Application.Abstractions.Messaging
{
    public interface IQuery<TResponse> : IRequest<TResponse>
    {
    }
}
