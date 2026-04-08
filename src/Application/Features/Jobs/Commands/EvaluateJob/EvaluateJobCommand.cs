using jobAgentApi.Application.Abstractions.Messaging;
using MediatR;
using jobAgentApi.Application.Repositories;
using jobAgentApi.Domain.Entities;

namespace jobAgentApi.Application.Features.Jobs.Commands.EvaluateJob;

public record EvaluateJobCommand(Guid JobId, Guid UserId, bool Liked, string? Feedback) : ICommand<Unit>;

public class EvaluateJobCommandHandler : ICommandHandler<EvaluateJobCommand, Unit>
{
    private readonly IUnitOfWork _unitOfWork;
    public EvaluateJobCommandHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;
    public async Task<Unit> Handle(EvaluateJobCommand request, CancellationToken cancellationToken)
    {
        var repo = _unitOfWork.GetRepository<JobEvaluation>();
        await repo.AddAsync(new JobEvaluation { JobId = request.JobId, UserId = request.UserId, Liked = request.Liked, Feedback = request.Feedback, CreatedAt = DateTime.UtcNow });
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
