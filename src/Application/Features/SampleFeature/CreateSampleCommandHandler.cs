using jobAgentApi.Application.Abstractions.Messaging;

namespace jobAgentApi.Application.Features.SampleFeature
{
    public sealed class CreateSampleCommandHandler : ICommandHandler<CreateSampleCommand, int>
    {
        public async Task<int> Handle(CreateSampleCommand request, CancellationToken cancellationToken)
        {
            // TODO: Criar a entidade e salvar no banco de dados
            return 1;
        }
    }
}
