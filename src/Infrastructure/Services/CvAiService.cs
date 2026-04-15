using System.Text.Json;
using jobAgentApi.Application.Abstractions;
using jobAgentApi.Domain.Entities; // or wherever DomainException is actually located
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Google.GenAI;
using Google.GenAI.Types;

namespace jobAgentApi.Infrastructure.Services;

public sealed class CvAiService : ICvAiService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<CvAiService> _logger;

    public CvAiService(IConfiguration configuration, ILogger<CvAiService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> GenerateTailoredCvAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var aiSection = _configuration.GetSection("AiSettings");

        var apiKey = aiSection["ApiKey"];
        var model = aiSection["Model"] ?? "gemini-3.1-flash-lite-preview";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new DomainException("AiSettings:ApiKey não configurado.", 500);
        }

        try
        {
            var client = new Client(apiKey: apiKey);

            var parameters = new GenerateContentConfig
            {
                Temperature = 0.2f
            };

            var response = await client.Models.GenerateContentAsync(
                model: model,
                contents: prompt,
                config: parameters
            );

            var responseText = response.Text;

            if (string.IsNullOrWhiteSpace(responseText))
            {
                throw new DomainException("A IA retornou conteúdo vazio.", 502);
            }

            return responseText.Trim();
        }
        catch (Exception ex) when (ex is not DomainException)
        {
            _logger.LogError(ex, "Gemini AI request failed.");
            throw new DomainException("Falha ao gerar currículo com a IA, tente novamente em alguns instantes.", 502);
        }
    }
}
