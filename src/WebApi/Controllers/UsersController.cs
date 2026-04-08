using System.Security.Claims;
using jobAgentApi.Application.Features.User.Commands.GenerateCv;
using jobAgentApi.Application.Features.User.Commands.UploadCv;
using jobAgentApi.Application.Features.User.Commands.SavePreferences;
using jobAgentApi.Application.Features.User.Queries.GetPreferences;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using jobAgentApi.Application.Features.User.Commands.EvaluateCv;

namespace jobAgentApi.WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly ISender _sender;

    public UsersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("preferences")]
    public async Task<IActionResult> SavePreferences([FromBody] SavePreferencesRequest request)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized("Usuário inválido.");
        }

        var command = new SavePreferencesCommand(request.Skills, request.Level, request.Area, userId);

        var result = await _sender.Send(command);

        return Ok(new { Id = result });
    }

    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized("Usuário inválido.");
        }

        var query = new GetPreferencesQuery(userId);
        var result = await _sender.Send(query);

        return Ok(result);
    }

    [HttpPost("cv")]
    public async Task<IActionResult> UploadCv(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("Arquivo não fornecido.");
        }

        if (file.ContentType != "application/pdf")
        {
            return BadRequest("Apenas arquivos PDF são permitidos.");
        }

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        
        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized("Usuário inválido.");
        }

        using var stream = file.OpenReadStream();
        var command = new UploadCvCommand(stream, file.FileName, file.ContentType, userId);
        
        var result = await _sender.Send(command);

        return Ok(new { Url = result });
    }

    [HttpPost("cv/generate")]
    public async Task<IActionResult> GenerateCv([FromBody] GenerateCvRequest request, CancellationToken cancellationToken)
    {
        if (request.JobId == Guid.Empty)
        {
            return BadRequest("O ID da vaga é obrigatório.");
        }

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized("Usuário inválido.");
        }

        var command = new GenerateCvCommand(request.JobId, userId);
        var result = await _sender.Send(command, cancellationToken);

        Response.Headers.Append("X-Generated-Cv-Url", result.StorageUrl);

        return File(result.PdfBytes, "application/pdf", result.FileName);
    }
    [HttpPost("cv/{id:guid}/evaluate")]
    public async Task<IActionResult> EvaluateCv(Guid id, [FromBody] EvaluateCvDto dto)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        if(!Guid.TryParse(userIdStr, out Guid userId)) return Unauthorized();

        var command = new EvaluateCvCommand(id, userId, dto.Liked, dto.Feedback);
        await _sender.Send(command);
        return Ok();
    }
}

public class EvaluateCvDto
{
    public bool Liked { get; set; }
    public string? Feedback { get; set; }
}

public record SavePreferencesRequest(List<string> Skills, string Level, string Area);
public record GenerateCvRequest(Guid JobId);
