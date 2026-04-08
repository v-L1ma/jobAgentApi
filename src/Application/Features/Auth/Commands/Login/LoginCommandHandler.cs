using jobAgentApi.Application.Abstractions;
using jobAgentApi.Application.Abstractions.Messaging;
using jobAgentApi.Application.Repositories;
using jobAgentApi.Domain.Entities;

namespace jobAgentApi.Application.Features.Auth.Commands.Login
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

            var userRepository = _unitOfWork.GetUserRepository();

            ApplicationUser? user = await userRepository.GetByEmailAsync(request.Email);

            if (user is null)
            {
                throw new DomainException("Email e/ou senha inválidos");
            }

            var validPassword = await userRepository.CheckPasswordAsync(user, request.Password);

            if (!validPassword)
            {
                throw new DomainException("Email e/ou senha inválidos");
            }

            var token = _tokenService.GenerateToken(user);
            var refreshToken = _tokenService.GenerateRefreshToken(user);

            return new LoginCommandResponse(token, refreshToken);
        }
    }
}
