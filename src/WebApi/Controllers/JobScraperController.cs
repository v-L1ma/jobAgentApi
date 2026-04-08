using jobAgentApi.Application.Features.Jobs.Commands.RunJobScraper;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace jobAgentApi.WebApi.Controllers;

[ApiController]
[Route("api/job-scraper")]
public class JobScraperController : ControllerBase
{
    private readonly ISender _sender;

    public JobScraperController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("run")]
    public async Task<IActionResult> Run([FromBody] RunJobScraperCommand command, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(command, cancellationToken);
        return Ok(response);
    }
}