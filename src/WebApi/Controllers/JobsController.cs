using jobAgentApi.Application.Features.Jobs.Queries.GetJobById;
using jobAgentApi.Application.Features.Jobs.Queries.GetJobs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using jobAgentApi.Application.Features.Jobs.Commands.EvaluateJob;

namespace jobAgentApi.WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/jobs")]
public class JobsController : ControllerBase
{
    private readonly ISender _sender;

    public JobsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetJobs(
        [FromQuery] string? stack,
        [FromQuery] string? location,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] bool triggerScraper = false)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        Guid.TryParse(userIdStr, out Guid userId);
        var query = new GetJobsQuery(stack, location, page, pageSize, userId, triggerScraper);
        var result = await _sender.Send(query);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetJobById(Guid id)
    {
        var query = new GetJobByIdQuery(id);
        var result = await _sender.Send(query);
        return Ok(result);
    }
    [HttpPost("{id:guid}/evaluate")]
    public async Task<IActionResult> EvaluateJob(Guid id, [FromBody] EvaluateJobDto dto)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        if(!Guid.TryParse(userIdStr, out Guid userId)) return Unauthorized();

        var command = new EvaluateJobCommand(id, userId, dto.Liked, dto.Feedback);
        await _sender.Send(command);
        return Ok();
    }
}

public class EvaluateJobDto
{
    public bool Liked { get; set; }
    public string? Feedback { get; set; }
}
