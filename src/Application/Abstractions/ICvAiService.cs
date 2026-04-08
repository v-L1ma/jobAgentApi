namespace jobAgentApi.Application.Abstractions;

public interface ICvAiService
{
    Task<string> GenerateTailoredCvAsync(string prompt, CancellationToken cancellationToken = default);
}
