using jobAgentApi.Application.Abstractions.Messaging;
using jobAgentApi.Application.Repositories;
using jobAgentApi.Domain.Entities;

namespace jobAgentApi.Application.Features.Auth.Commands.Register
{
    public sealed class RegisterCommandHandler : ICommandHandler<RegisterCommand, Guid>
    {
        private readonly IUnitOfWork _unitOfWork;

        public RegisterCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Guid> Handle(RegisterCommand request, CancellationToken cancellationToken)
        {
            var userRepository = _unitOfWork.GetUserRepository();

            var existingUser = await userRepository.GetByEmailAsync(request.Email);
            if (existingUser is not null)
            {
                throw new DomainException("Usuário com este e-mail já existe.", 409);
            }

            var newUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Email = request.Email,
                OnboardingCompleted = false,
                // CPF não está no comando de registro atual, deixamos em branco ou ajustamos se necessário
            };

            var success = await userRepository.CreateWithPasswordAsync(newUser, request.Password);

            if (!success)
            {
                throw new DomainException("Erro ao criar o usuário. Verifique os requisitos de senha.", 400);
            }

            // Nota: Dependendo da configuração do UoW e do Identity, SaveChangesAsync pode ser necessário
            // se o UserRepository não persistir imediatamente no banco de dados.
            // No caso do UserRepository atual, ele usa UserManager que persiste diretamente.
            
            return newUser.Id;
        }
    }
}