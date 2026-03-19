using jobAgentApi.Application.Abstractions.Messaging;

namespace jobAgentApi.Application.Features.SampleFeature
{
    public record CreateSampleCommand(string Name) : ICommand<int>;
}
