using jobAgentApi.Application.Abstractions.Messaging;
using MediatR;
using jobAgentApi.Application.Repositories;
using jobAgentApi.Domain.Entities;

namespace jobAgentApi.Application.Features.User.Commands.EvaluateCv;

public record EvaluateCvCommand(Guid GeneratedCvId, Guid UserId, bool Liked, string? Feedback) : ICommand<Unit>;

public class EvaluateCvCommandHandler : ICommandHandler<EvaluateCvCommand, Unit>
{
    private readonly IUnitOfWork _unitOfWork;
    public EvaluateCvCommandHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;
    public async Task<Unit> Handle(EvaluateCvCommand request, CancellationToken cancellationToken)
    {
        var repo = _unitOfWork.GetRepository<CvEvaluation>();
        await repo.AddAsync(new CvEvaluation { GeneratedCvId = request.GeneratedCvId, UserId = request.UserId, Liked = request.Liked, Feedback = request.Feedback, CreatedAt = DateTime.UtcNow });
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
