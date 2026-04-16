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

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new DomainException("AiSettings:ApiKey não configurado.", 500);
        }

        string[] fallbackModels = new[]
        {
            "gemini-3.1-flash-lite-preview",
            "gemma-4-31b-it",
            "gemini-2.5-flash-lite",
            "gemini-2.5-pro",
            "gemini-2.5-flash"
        };

        var client = new Client(apiKey: apiKey);

        var parameters = new GenerateContentConfig
        {
            Temperature = 0.2f
        };

        foreach (var model in fallbackModels)
        {
            try
            {
                var response = await client.Models.GenerateContentAsync(
                    model: model,
                    contents: prompt,
                    config: parameters
                );

                var responseText = response.Text;

                if (!string.IsNullOrWhiteSpace(responseText))
                {
                    return responseText.Trim();
                }

                _logger.LogWarning("O modelo {Model} retornou conteúdo vazio. Tentando o próximo...", model);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao request no modelo {Model}. Tentando o próximo...", model);
            }
        }

        _logger.LogError("Todos os modelos de fallback do Gemini falharam.");
        throw new DomainException("Falha ao gerar currículo com a IA, tente novamente em alguns instantes.", 502);
    }
}
