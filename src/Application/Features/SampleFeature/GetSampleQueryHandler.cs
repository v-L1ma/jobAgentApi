using jobAgentApi.Application.Abstractions.Messaging;

namespace jobAgentApi.Application.Features.SampleFeature
{
    public sealed class GetSampleQueryHandler : IQueryHandler<GetSampleQuery, string>
    {
        public async Task<string> Handle(GetSampleQuery request, CancellationToken cancellationToken)
        {
            // TODO: Buscar do banco de dados
            return "Sample Data";
        }
    }
}
