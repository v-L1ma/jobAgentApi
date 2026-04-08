using jobAgentApi.Application.Abstractions;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace jobAgentApi.Infrastructure.Services;

public partial class PdfService : IPdfService
{
    private readonly ILogger<PdfService> _logger;

    public PdfService(ILogger<PdfService> logger)
    {
        _logger = logger;
    }

    public async Task<string> ExtractTextAsync(Stream pdfStream)
    {
        if (pdfStream is null)
            throw new ArgumentNullException(nameof(pdfStream));

        _logger.LogInformation("Extracting text from PDF stream");

        var streamToRead = pdfStream;

        if (!pdfStream.CanSeek)
        {
            var bufferedStream = new MemoryStream();
            await pdfStream.CopyToAsync(bufferedStream);
            bufferedStream.Position = 0;
            streamToRead = bufferedStream;
        }

        var originalPosition = streamToRead.CanSeek ? streamToRead.Position : 0;

        try
        {
            if (streamToRead.CanSeek)
                streamToRead.Position = 0;

            using var document = PdfDocument.Open(streamToRead);
            var textBuilder = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                var words = page.GetWords()
                    .OrderByDescending(w => w.BoundingBox.Bottom) // top → bottom
                    .ThenBy(w => w.BoundingBox.Left)              // left → right
                    .ToList();

                var lines = new List<List<Word>>();
                const double lineTolerance = 3; // ajuste fino se precisar

                foreach (var word in words)
                {
                    var line = lines.FirstOrDefault(l =>
                        Math.Abs(l.First().BoundingBox.Bottom - word.BoundingBox.Bottom) < lineTolerance);

                    if (line == null)
                    {
                        line = new List<Word>();
                        lines.Add(line);
                    }

                    line.Add(word);
                }

                foreach (var line in lines)
                {
                    var orderedLine = line.OrderBy(w => w.BoundingBox.Left).ToList();

                    for (int i = 0; i < orderedLine.Count; i++)
                    {
                        var current = orderedLine[i];
                        textBuilder.Append(current.Text);

                        if (i < orderedLine.Count - 1)
                        {
                            var next = orderedLine[i + 1];

                            var gap = next.BoundingBox.Left - current.BoundingBox.Right;

                            if (gap > 2) // heurística de espaço
                                textBuilder.Append(" ");
                        }
                    }

                    textBuilder.AppendLine();
                }

                textBuilder.AppendLine(); // quebra entre páginas
            }

            var result = textBuilder.ToString();

            // 🔧 Normalização final
            result = NormalizeText(result);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while extracting text from PDF.");
            throw;
        }
        finally
        {
            if (streamToRead.CanSeek)
                streamToRead.Position = originalPosition;

            if (!ReferenceEquals(streamToRead, pdfStream))
                await streamToRead.DisposeAsync();
        }
    }
    public Task<byte[]> GeneratePdfAsync(string textContent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(textContent))
        {
            throw new ArgumentException("O conteúdo do currículo não pode ser vazio.", nameof(textContent));
        }

        cancellationToken.ThrowIfCancellationRequested();

        QuestPDF.Settings.License = LicenseType.Community;

        if (TryParseGeneratedCv(textContent, out var generatedCv))
        {
            var structuredPdf = Document
                .Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(28);

                        page.Header().Column(column =>
                        {
                            column.Item().Text(EmptyIfNull(generatedCv.Nome, "Currículo"))
                                .SemiBold()
                                .FontSize(20);

                            var contactParts = new[]
                            {
                                generatedCv.Email,
                                generatedCv.Telefone,
                                generatedCv.Linkedin,
                                generatedCv.Github
                            }
                            .Where(static value => !string.IsNullOrWhiteSpace(value))
                            .ToList();

                            if (contactParts.Count > 0)
                            {
                                column.Item().PaddingTop(4).Text(string.Join(" | ", contactParts!)).FontSize(10);
                            }
                        });

                        page.Content().PaddingVertical(10).Column(column =>
                        {
                            column.Spacing(12);

                            if (!string.IsNullOrWhiteSpace(generatedCv.Resumo))
                            {
                                column.Item().Text("Resumo").SemiBold().FontSize(13);
                                column.Item().Text(generatedCv.Resumo!).FontSize(11);
                            }

                            if (generatedCv.Skills.Count > 0)
                            {
                                column.Item().Text("Skills").SemiBold().FontSize(13);
                                column.Item().Text(string.Join(" | ", generatedCv.Skills)).FontSize(11);
                            }

                            if (generatedCv.Experiencias.Count > 0)
                            {
                                column.Item().Text("Experiencias").SemiBold().FontSize(13);

                                foreach (var experiencia in generatedCv.Experiencias)
                                {
                                    var cargoEmpresa = string.Join(" - ", new[] { experiencia.Cargo, experiencia.Empresa }
                                        .Where(static value => !string.IsNullOrWhiteSpace(value)));
                                    if (!string.IsNullOrWhiteSpace(cargoEmpresa))
                                    {
                                        column.Item().Text(cargoEmpresa).SemiBold().FontSize(11);
                                    }

                                    var periodo = string.Join(" a ", new[] { experiencia.DataInicio, experiencia.DataFim }
                                        .Where(static value => !string.IsNullOrWhiteSpace(value)));
                                    if (!string.IsNullOrWhiteSpace(periodo))
                                    {
                                        column.Item().Text(periodo).FontSize(10);
                                    }

                                    if (!string.IsNullOrWhiteSpace(experiencia.Descricao))
                                    {
                                        column.Item().Text(experiencia.Descricao!).FontSize(11);
                                    }
                                }
                            }

                            if (generatedCv.Educacao.Count > 0)
                            {
                                column.Item().Text("Educacao").SemiBold().FontSize(13);

                                foreach (var educacao in generatedCv.Educacao)
                                {
                                    var cursoInstituicao = string.Join(" - ", new[] { educacao.Curso, educacao.Instituicao }
                                        .Where(static value => !string.IsNullOrWhiteSpace(value)));
                                    if (!string.IsNullOrWhiteSpace(cursoInstituicao))
                                    {
                                        column.Item().Text(cursoInstituicao).SemiBold().FontSize(11);
                                    }

                                    var periodo = string.Join(" a ", new[] { educacao.DataInicio, educacao.DataFim }
                                        .Where(static value => !string.IsNullOrWhiteSpace(value)));
                                    if (!string.IsNullOrWhiteSpace(periodo))
                                    {
                                        column.Item().Text(periodo).FontSize(10);
                                    }
                                }
                            }
                        });

                        page.Footer()
                            .AlignCenter()
                            .Text($"Gerado em {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC")
                            .FontSize(9);
                    });
                })
                .GeneratePdf();

            return Task.FromResult(structuredPdf);
        }

        var pdfBytes = Document
            .Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(28);

                    page.Header()
                        .Text("Currículo Personalizado")
                        .SemiBold()
                        .FontSize(16);

                    page.Content()
                        .PaddingVertical(8)
                        .Text(textContent)
                        .FontSize(11);

                    page.Footer()
                        .AlignCenter()
                        .Text($"Gerado em {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC")
                        .FontSize(9);
                });
            })
            .GeneratePdf();

        return Task.FromResult(pdfBytes);
    }

    private static bool TryParseGeneratedCv(string rawJson, out GeneratedCvDocument generatedCv)
    {
        generatedCv = new GeneratedCvDocument();

        try
        {
            var sanitizedJson = StripMarkdownCodeFence(rawJson);

            using var jsonDocument = JsonDocument.Parse(sanitizedJson);
            if (jsonDocument.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            generatedCv = JsonSerializer.Deserialize<GeneratedCvDocument>(sanitizedJson, options) ?? new GeneratedCvDocument();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string StripMarkdownCodeFence(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var lines = trimmed
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .ToList();

        if (lines.Count == 0)
        {
            return trimmed;
        }

        if (lines[0].StartsWith("```", StringComparison.Ordinal))
        {
            lines.RemoveAt(0);
        }

        if (lines.Count > 0 && lines[^1].Trim().Equals("```", StringComparison.Ordinal))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return string.Join('\n', lines).Trim();
    }

    private static string EmptyIfNull(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private string NormalizeText(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        // Remove caracteres bugados comuns vindos do overleaf
        input = input.Replace("c¸", "ç")
                    .Replace("˜a", "ã")
                    .Replace("˜o", "õ")
                    .Replace("´a", "á")
                    .Replace("´e", "é")
                    .Replace("´i", "í")
                    .Replace("´o", "ó")
                    .Replace("´u", "ú")
                    .Replace("ˆe", "ê")
                    .Replace("ˆ", "")
                    .Replace("´", "")
                    .Replace("˜", "")
                    .Replace("¸", "");
                    
        input = FixCamelCaseRegex().Replace(input, "$1 $2");

        input = NumberAndLettersRegex().Replace(input, "$1 $2");
        input = NumberAndLettersRegex2().Replace(input, "$1 $2");

        input = MultiplesEspecesRegex().Replace(input, " ");

        input = NormalizeWordBreaksRegex().Replace(input, "\n\n");

        return input.Trim();
    }

    [GeneratedRegex(@"([a-z])([A-Z])")]
    private static partial Regex FixCamelCaseRegex();
    [GeneratedRegex(@"(\d)([A-Za-z])")]
    private static partial Regex NumberAndLettersRegex();
    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex NormalizeWordBreaksRegex();
    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultiplesEspecesRegex();
    [GeneratedRegex(@"([A-Za-z])(\d)")]
    private static partial Regex NumberAndLettersRegex2();

    private sealed class GeneratedCvDocument
    {
        [JsonPropertyName("nome")]
        public string? Nome { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("telefone")]
        public string? Telefone { get; set; }

        [JsonPropertyName("linkedin")]
        public string? Linkedin { get; set; }

        [JsonPropertyName("github")]
        public string? Github { get; set; }

        [JsonPropertyName("resumo")]
        public string? Resumo { get; set; }

        [JsonPropertyName("skills")]
        public List<string> Skills { get; set; } = [];

        [JsonPropertyName("experiencias")]
        public List<GeneratedCvExperience> Experiencias { get; set; } = [];

        [JsonPropertyName("educacao")]
        public List<GeneratedCvEducation> Educacao { get; set; } = [];
    }

    private sealed class GeneratedCvExperience
    {
        [JsonPropertyName("empresa")]
        public string? Empresa { get; set; }

        [JsonPropertyName("cargo")]
        public string? Cargo { get; set; }

        [JsonPropertyName("dataInicio")]
        public string? DataInicio { get; set; }

        [JsonPropertyName("dataFim")]
        public string? DataFim { get; set; }

        [JsonPropertyName("descricao")]
        public string? Descricao { get; set; }
    }

    private sealed class GeneratedCvEducation
    {
        [JsonPropertyName("instituicao")]
        public string? Instituicao { get; set; }

        [JsonPropertyName("curso")]
        public string? Curso { get; set; }

        [JsonPropertyName("dataInicio")]
        public string? DataInicio { get; set; }

        [JsonPropertyName("dataFim")]
        public string? DataFim { get; set; }
    }
}

