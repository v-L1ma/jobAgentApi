using jobAgentApi.Application.Abstractions.Messaging;
using jobAgentApi.Application.Repositories;
using System.Linq;

namespace jobAgentApi.Application.Features.User.Queries.GetUserProfile;

public sealed class GetUserProfileQueryHandler : IQueryHandler<GetUserProfileQuery, GetUserProfileResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetUserProfileQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<GetUserProfileResponse> Handle(GetUserProfileQuery request, CancellationToken cancellationToken)
    {
        var userRepository = _unitOfWork.GetUserRepository();
        var user = await userRepository.GetByIdAsync(request.UserId);

        if (user is null)
        {
            throw new DomainException("Usuário não encontrado.", 404);
        }

        return new GetUserProfileResponse(
            user.Id,
            user.Name,
            user.Email,
            MaskCpf(user.CPF));
    }

    private static string? MaskCpf(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
        {
            return null;
        }

        var digits = new string(cpf.Where(char.IsDigit).ToArray());

        if (digits.Length != 11)
        {
            return null;
        }

        return $"{digits[..3]}.***.{digits[6..9]}-{digits[9..]}";
    }
}