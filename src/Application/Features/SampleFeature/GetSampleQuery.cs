using jobAgentApi.Application.Abstractions.Messaging;

namespace jobAgentApi.Application.Features.SampleFeature
{
    public record GetSampleQuery(int Id) : IQuery<string>;
}
