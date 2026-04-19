using jobAgentApi.Application.Abstractions.Messaging;

namespace jobAgentApi.Application.Features.User.Commands.CompleteOnboarding;

public sealed record CompleteOnboardingCommand(Guid UserId) : ICommand<CompleteOnboardingResponse>;

public sealed record CompleteOnboardingResponse(bool IsFirstAccess);
