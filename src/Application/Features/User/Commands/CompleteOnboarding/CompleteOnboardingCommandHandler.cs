using jobAgentApi.Application.Abstractions.Messaging;
using jobAgentApi.Application.Repositories;
using jobAgentApi.Domain.Entities;

namespace jobAgentApi.Application.Features.User.Commands.CompleteOnboarding;

public sealed class CompleteOnboardingCommandHandler : ICommandHandler<CompleteOnboardingCommand, CompleteOnboardingResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserSearchQueryRepository _userSearchQueryRepository;

    public CompleteOnboardingCommandHandler(
        IUnitOfWork unitOfWork,
        IUserSearchQueryRepository userSearchQueryRepository)
    {
        _unitOfWork = unitOfWork;
        _userSearchQueryRepository = userSearchQueryRepository;
    }

    public async Task<CompleteOnboardingResponse> Handle(CompleteOnboardingCommand request, CancellationToken cancellationToken)
    {
        var userRepository = _unitOfWork.GetUserRepository();
        var user = await userRepository.GetByIdAsync(request.UserId);

        if (user is null)
        {
            throw new DomainException("Usuário não encontrado.", 404);
        }

        var hasPreferences = await _userSearchQueryRepository.HasUserQueriesAsync(request.UserId, cancellationToken);

        var userCvRepository = _unitOfWork.GetRepository<UserCv>();
        var allUserCvs = await userCvRepository.GetAllAsync();
        var hasUploadedCv = allUserCvs.Any(cv =>
            cv.UserId == request.UserId &&
            cv.Active &&
            !string.IsNullOrWhiteSpace(cv.ExtractedText));

        if (!hasPreferences || !hasUploadedCv)
        {
            var pendingSteps = new List<string>();

            if (!hasPreferences)
            {
                pendingSteps.Add("preferências");
            }

            if (!hasUploadedCv)
            {
                pendingSteps.Add("currículo");
            }

            var missingSteps = string.Join(" e ", pendingSteps);
            throw new DomainException($"Finalize {missingSteps} antes de concluir o onboarding.", 400);
        }

        if (!user.OnboardingCompleted)
        {
            user.OnboardingCompleted = true;

            var updated = await userRepository.UpdateAsync(user);
            if (!updated)
            {
                throw new DomainException("Não foi possível concluir o onboarding.", 400);
            }
        }

        return new CompleteOnboardingResponse(false);
    }
}
