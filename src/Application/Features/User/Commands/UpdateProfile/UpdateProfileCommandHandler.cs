using System.Net.Mail;
using jobAgentApi.Application.Abstractions.Messaging;
using jobAgentApi.Application.Repositories;

namespace jobAgentApi.Application.Features.User.Commands.UpdateProfile;

public sealed class UpdateProfileCommandHandler : ICommandHandler<UpdateProfileCommand, Guid>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateProfileCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var userRepository = _unitOfWork.GetUserRepository();

        var user = await userRepository.GetByIdAsync(request.UserId);
        if (user is null)
        {
            throw new DomainException("Usuário não encontrado.", 404);
        }

        var hasProfileChanges = false;

        var normalizedName = request.Name?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedName))
        {
            user.Name = normalizedName;
            hasProfileChanges = true;
        }

        var normalizedEmail = request.Email?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedEmail))
        {
            if (!IsValidEmail(normalizedEmail))
            {
                throw new DomainException("Email inválido.", 400);
            }

            if (!string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            {
                var existingUser = await userRepository.GetByEmailAsync(normalizedEmail);
                if (existingUser is not null && existingUser.Id != user.Id)
                {
                    throw new DomainException("Usuário com este e-mail já existe.", 409);
                }

                user.Email = normalizedEmail;
                hasProfileChanges = true;
            }
        }

        var hasCurrentPassword = !string.IsNullOrWhiteSpace(request.CurrentPassword);
        var hasNewPassword = !string.IsNullOrWhiteSpace(request.NewPassword);
        var hasConfirmNewPassword = !string.IsNullOrWhiteSpace(request.ConfirmNewPassword);
        var wantsPasswordChange = hasCurrentPassword || hasNewPassword || hasConfirmNewPassword;

        if (wantsPasswordChange)
        {
            if (!hasCurrentPassword || !hasNewPassword || !hasConfirmNewPassword)
            {
                throw new DomainException("Para alterar a senha, informe senhaAtual, novaSenha e confirmarNovaSenha.", 400);
            }

            if (!string.Equals(request.NewPassword, request.ConfirmNewPassword, StringComparison.Ordinal))
            {
                throw new DomainException("A confirmação da nova senha não confere.", 400);
            }

            var validCurrentPassword = await userRepository.CheckPasswordAsync(user, request.CurrentPassword!);
            if (!validCurrentPassword)
            {
                throw new DomainException("Senha atual inválida.", 400);
            }
        }

        if (wantsPasswordChange)
        {
            var passwordChanged = await userRepository.ChangePasswordAsync(user, request.CurrentPassword!, request.NewPassword!);
            if (!passwordChanged)
            {
                throw new DomainException("Não foi possível alterar a senha. Verifique os requisitos de senha.", 400);
            }
        }

        if (hasProfileChanges)
        {
            var updated = await userRepository.UpdateAsync(user);
            if (!updated)
            {
                throw new DomainException("Não foi possível atualizar o perfil.", 400);
            }
        }

        return user.Id;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var parsed = new MailAddress(email);
            return string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
