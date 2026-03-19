using jobAgentApi.Application.Abstractions;
using jobAgentApi.Application.Abstractions.Messaging;
using jobAgentApi.Application.Repositories;

namespace jobAgentApi.Application.Features.Auth.Commands
{
    public sealed class LoginCommandHandler : ICommandHandler<LoginCommand, LoginCommandResponse>
    {
        private readonly ITokenService _tokenService;
        private readonly IUnitOfWork _unitOfWork;
        public LoginCommandHandler(ITokenService tokenService, IUnitOfWork unitOfWork)
        {
            _tokenService = tokenService;
            _unitOfWork = unitOfWork;
        }

        public async Task<LoginCommandResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                throw new DomainException("Por favor informe um email");
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                throw new DomainException("Por favor informe uma Senha");
            }

            
        }
    }
}
