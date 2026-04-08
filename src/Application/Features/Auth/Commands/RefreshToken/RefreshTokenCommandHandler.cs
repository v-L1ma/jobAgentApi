using System.Security.Claims;
using jobAgentApi.Application.Abstractions;
using jobAgentApi.Application.Abstractions.Messaging;
using jobAgentApi.Application.Repositories;
using Microsoft.IdentityModel.JsonWebTokens;

namespace jobAgentApi.Application.Features.Auth.Commands.RefreshToken
{
    public sealed class RefreshTokenCommandHandler : ICommandHandler<RefreshTokenCommand, RefreshTokenResponse>
    {
        private readonly ITokenService _tokenService;
        private readonly IUnitOfWork _unitOfWork;

        public RefreshTokenCommandHandler(ITokenService tokenService, IUnitOfWork unitOfWork)
        {
            _tokenService = tokenService;
            _unitOfWork = unitOfWork;
        }

        public async Task<RefreshTokenResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
        {
            var isRefreshTokenValid = await _tokenService.ValidateToken(request.RefreshToken);
            if (!isRefreshTokenValid)
            {
                throw new DomainException("Refresh Token inválido ou expirado.", 401);
            }

            var principal = _tokenService.GetPrincipalFromExpiredToken(request.Token);
            if (principal is null)
            {
                throw new DomainException("Token de acesso inválido.", 400);
            }

            var userEmail = principal.FindFirst(ClaimTypes.Email) ?? principal.FindFirst(JwtRegisteredClaimNames.Email);
            if (string.IsNullOrEmpty(userEmail?.Value))
            {
                throw new DomainException("Token inválido: informações de usuário não encontradas.", 400);
            }

            var userRepository = _unitOfWork.GetUserRepository();
            var user = await userRepository.GetByEmailAsync(userEmail.Value);
            if (user is null)
            {
                throw new DomainException("Usuário não encontrado.", 404);
            }

            // 5. Gerar novo par de tokens
            var newToken = _tokenService.GenerateToken(user);
            var newRefreshToken = _tokenService.GenerateRefreshToken(user);

            return new RefreshTokenResponse(newToken, newRefreshToken);
        }
    }
}