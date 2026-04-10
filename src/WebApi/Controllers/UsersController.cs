using System.Security.Claims;
using System.Globalization;
using jobAgentApi.Application.Features.User.Commands.GenerateCv;
using jobAgentApi.Application.Features.User.Commands.UploadCv;
using jobAgentApi.Application.Features.User.Commands.SavePreferences;
using jobAgentApi.Application.Features.User.Queries.GetPreferences;
using jobAgentApi.Application.Features.User.Queries.GetUserStatistics;
using jobAgentApi.Application.Features.User.Queries.GetUserCv;
using jobAgentApi.Application.Features.User.Queries.GetGeneratedCvs;
using jobAgentApi.Application.Abstractions.Messaging;
using jobAgentApi.Application.Repositories;
using jobAgentApi.Domain.Entities;
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
    private readonly IUnitOfWork _unitOfWork;

    public UsersController(ISender sender, IUnitOfWork unitOfWork)
    {
        _sender = sender;
        _unitOfWork = unitOfWork;
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

    /// <summary>
    /// Obtém o currículo armazenado do usuário autenticado para visualização.
    /// Retorna o PDF do currículo gerado a partir dos dados armazenados.
    /// </summary>
    [HttpGet("cv")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserCv()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized("Usuário inválido.");
        }

        var query = new GetUserCvQuery(userId);
        var result = await _sender.Send(query);

        if (!result.HasCv)
        {
            return NotFound("Nenhum currículo encontrado para este usuário.");
        }

        if (!string.IsNullOrWhiteSpace(result.FileName))
        {
            Response.Headers.Append("X-Cv-File-Name", result.FileName);
        }

        if (result.UploadedAtUtc.HasValue)
        {
            Response.Headers.Append("X-Cv-Upload-Date", result.UploadedAtUtc.Value.ToString("O"));
        }

        if (result.FileSizeBytes.HasValue)
        {
            Response.Headers.Append("X-Cv-File-Size-Bytes", result.FileSizeBytes.Value.ToString(CultureInfo.InvariantCulture));
        }

        Response.Headers.Append("Access-Control-Expose-Headers", "X-Cv-File-Name,X-Cv-Upload-Date,X-Cv-File-Size-Bytes");

        return File(result.PdfBytes!, "application/pdf", result.FileName!);
    }

    /// <summary>
    /// Lista todos os currículos gerados para o usuário autenticado.
    /// </summary>
    [HttpGet("generated-cvs")]
    [ProducesResponseType(typeof(GetGeneratedCvsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGeneratedCvs()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized("Usuário inválido.");
        }

        var query = new GetGeneratedCvsQuery(userId);
        var result = await _sender.Send(query);

        return Ok(result);
    }

    /// <summary>
    /// Obtém um currículo gerado específico para download.
    /// </summary>
    [HttpGet("generated-cvs/{id:guid}")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGeneratedCvById(Guid id)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized("Usuário inválido.");
        }

        var generatedCvRepository = _unitOfWork.GetRepository<GeneratedCv>();
        var allGeneratedCvs = await generatedCvRepository.GetAllAsync();
        var generatedCv = allGeneratedCvs.FirstOrDefault(cv => cv.Id == id && cv.UserId == userId && cv.Active);

        if (generatedCv is null)
        {
            return NotFound("Currículo gerado não encontrado.");
        }

        // Note: For now we return a redirect to the stored URL
        // In a real scenario, you might fetch the file from storage and return it directly
        return Redirect(generatedCv.UrlFile);
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

    /// <summary>
    /// Busca estatísticas detalhadas do usuário sobre candidaturas.
    /// Inclui visão geral, distribuição por status, plataforma e candidaturas por dia.
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(UserStatisticsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserStatistics()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        
        if (!Guid.TryParse(userIdStr, out Guid userId))
        {
            return Unauthorized("Usuário inválido.");
        }

        var query = new GetUserStatisticsQuery(userId);
        var result = await _sender.Send(query);

        return Ok(result);
    }
}

public class EvaluateCvDto
{
    public bool Liked { get; set; }
    public string? Feedback { get; set; }
}

public record SavePreferencesRequest(List<string> Skills, string Level, string Area);
public record GenerateCvRequest(Guid JobId);
