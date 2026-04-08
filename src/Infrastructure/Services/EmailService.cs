using jobAgentApi.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace jobAgentApi.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;

    public EmailService(ILogger<EmailService> logger)
    {
        _logger = logger;
    }

    public async Task SendPasswordResetEmailAsync(string email, string token)
    {
        // TODO: Implement actual email sending (SMTP, SendGrid, etc.)
        _logger.LogInformation("Sending password reset email to {Email} with token {Token}", email, token);
        await Task.CompletedTask;
    }
}
