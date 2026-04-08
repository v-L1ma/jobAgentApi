using jobAgentApi.Application.Abstractions;
using jobAgentApi.Application.Abstractions.Messaging;
using jobAgentApi.Application.Repositories;

namespace jobAgentApi.Application.Features.Auth.Commands.ForgotPassword
{
    public sealed class ForgotPasswordCommandHandler : ICommandHandler<ForgotPasswordCommand, string>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;

        public ForgotPasswordCommandHandler(IUnitOfWork unitOfWork, IEmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _emailService = emailService;
        }

        public async Task<string> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
        {
            var userRepository = _unitOfWork.GetUserRepository();
            var user = await userRepository.GetByEmailAsync(request.Email);

            // Por questões de segurança, não revelamos se o e-mail existe ou não
            if (user is null)
            {
                return "Caso exista um email cadastrado, será enviado um email para o mesmo.";
            }

            var token = await userRepository.GeneratePasswordResetTokenAsync(user);

            // TODO: Enviar o e-mail com o token para o usuário (via broker ou serviço de e-mail)
            await _emailService.SendPasswordResetEmailAsync(user.Email, token);

            return "Caso exista um email cadastrado, será enviado um email para o mesmo.";
        }
    }
}