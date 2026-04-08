using jobAgentApi.Application.Abstractions.Messaging;

namespace jobAgentApi.Application.Features.Jobs.Queries.GetJobById;

public record GetJobByIdQuery(Guid Id) : IQuery<JobResponse>;

public record JobResponse(
    Guid Id,
    string PlataformJobId,
    string Title,
    string Description,
    string Url,
    bool IsApplied,
    JobAnalysisResponse? Analysis);

public record JobAnalysisResponse(
    string Skills,
    string Nivel,
    string Keywords);
