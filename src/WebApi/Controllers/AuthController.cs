using jobAgentApi.Application.Features.Auth.Commands.Login;
using jobAgentApi.Application.Features.Auth.Commands.Register;
using jobAgentApi.Application.Features.Auth.Commands.RefreshToken;
using jobAgentApi.Application.Features.Auth.Commands.ForgotPassword;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace jobAgentApi.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AuthController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterCommand command, CancellationToken cancellationToken)
        {
             var result = await _mediator.Send(command, cancellationToken);
             return Ok(result);
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenCommand command, CancellationToken cancellationToken)
        {
             var result = await _mediator.Send(command, cancellationToken);
             return Ok(result);
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordCommand command, CancellationToken cancellationToken)
        {
             var result = await _mediator.Send(command, cancellationToken);
             return Ok(result);
        }
    }
}
