using jobAgentApi.Application.Abstractions.Messaging;
using jobAgentApi.Application.Repositories;
using jobAgentApi.Domain.Entities;

namespace jobAgentApi.Application.Features.Jobs.Queries.GetJobById;

public sealed class GetJobByIdQueryHandler : IQueryHandler<GetJobByIdQuery, JobResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetJobByIdQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<JobResponse> Handle(GetJobByIdQuery request, CancellationToken cancellationToken)
    {
        var repository = _unitOfWork.GetRepository<Job>();
        var job = await repository.GetByIdAsync(request.Id);

        if (job is null)
        {
            throw new DomainException("Vaga não encontrada.", 404);
        }

        return new JobResponse(
            job.Id,
            job.PlataformJobId,
            job.Title,
            job.Company,
            job.Platform,
            job.Description,
            job.Url,
            job.IsApplied,
            job.Analysis != null ? new JobAnalysisResponse(job.Analysis.Skills, job.Analysis.Nivel, job.Analysis.Keywords) : null
        );
    }
}
