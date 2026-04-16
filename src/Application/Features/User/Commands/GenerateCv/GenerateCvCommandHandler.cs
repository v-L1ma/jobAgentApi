using System.Text;
using jobAgentApi.Application.Abstractions;
using jobAgentApi.Application.Abstractions.Messaging;
using jobAgentApi.Application.Repositories;
using jobAgentApi.Domain.Entities;

namespace jobAgentApi.Application.Features.User.Commands.GenerateCv;

public sealed class GenerateCvCommandHandler : ICommandHandler<GenerateCvCommand, GenerateCvResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICvAiService _cvAiService;
    private readonly IPdfService _pdfService;
    private readonly IStorageService _storageService;

    public GenerateCvCommandHandler(
        IUnitOfWork unitOfWork,
        ICvAiService cvAiService,
        IPdfService pdfService,
        IStorageService storageService)
    {
        _unitOfWork = unitOfWork;
        _cvAiService = cvAiService;
        _pdfService = pdfService;
        _storageService = storageService;
    }

    public async Task<GenerateCvResponse> Handle(GenerateCvCommand request, CancellationToken cancellationToken)
    {
        var jobRepository = _unitOfWork.GetRepository<Job>();
        var job = await jobRepository.GetByIdAsync(request.JobId);

        if (job is null)
        {
            throw new DomainException("Vaga não encontrada.", 404);
        }

        var userCvRepository = _unitOfWork.GetRepository<UserCv>();
        var userCvs = await userCvRepository.GetAllAsync();

        var latestUserCv = userCvs
            .Where(cv => cv.UserId == request.UserId)
            .OrderByDescending(cv => cv.CreatedAt)
            .ThenByDescending(cv => cv.Id)
            .FirstOrDefault();

        if (latestUserCv is null)
        {
            throw new DomainException("Nenhum currículo foi enviado por este usuário.", 404);
        }

        if (string.IsNullOrWhiteSpace(latestUserCv.ExtractedText))
        {
            throw new DomainException("Não foi encontrado texto extraído do currículo para gerar uma nova versão.", 400);
        }

        var prompt = BuildPrompt(job.Title, job.Description, latestUserCv.ExtractedText);

        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine("Prompt para geração de CV:");
        Console.WriteLine(prompt);
        Console.WriteLine();
        Console.WriteLine();
        var generatedCvText = await _cvAiService.GenerateTailoredCvAsync(prompt, cancellationToken);

        if (string.IsNullOrWhiteSpace(generatedCvText))
        {
            throw new DomainException("A IA retornou um currículo vazio.", 502);
        }

        var pdfBytes = await _pdfService.GeneratePdfAsync(generatedCvText, cancellationToken);

        var safeJobTitle = SanitizeFileName(job.Title);
        var fileName = $"cv-{safeJobTitle}-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
        var path = $"generated-cvs/{request.UserId}/{Guid.NewGuid()}-{fileName}";

        await using var pdfStream = new MemoryStream(pdfBytes);
        var fileUrl = await _storageService.UploadFileAsync(pdfStream, path, "application/pdf");

        var generatedCvRepository = _unitOfWork.GetRepository<GeneratedCv>();

        var generatedCv = new GeneratedCv
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            UrlFile = fileUrl,
            CreatedAt = DateTime.UtcNow,
            Active = true,
            LastModifiedAt = DateTime.UtcNow,
            LastModifiedBy = request.UserId.ToString(),
            CreatedBy = request.UserId.ToString()
        };

        await generatedCvRepository.AddAsync(generatedCv);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new GenerateCvResponse(pdfBytes, fileName, fileUrl);
    }

    private static string BuildPrompt(string jobTitle, string jobDescription, string originalCvText)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Você é um especialista em recrutamento técnico e escrita de currículos.");
        builder.AppendLine("Sua tarefa é reescrever o currículo para aumentar as chances do candidato passar nesta vaga.");
        builder.AppendLine("Regras:");
        builder.AppendLine("1) Não invente experiências, tecnologias ou formações que não possam ser inferidas do currículo original.");
        builder.AppendLine("2) Foque em aderência com requisitos da vaga, palavras-chave e resultados mensuráveis quando existirem.");
        builder.AppendLine("3) Escreva em português do Brasil, de forma profissional e objetiva.");
        builder.AppendLine("4) Retorne SOMENTE o conteúdo final do currículo em json no formato informado, sem explicações adicionais.");
        builder.AppendLine();
        builder.AppendLine("REGRA CRÍTICA:");
        builder.AppendLine("NUNCA invente ou complete dados que não estejam explícitos no currículo original.");
        builder.AppendLine("Se uma informação não existir no currículo original, retorne string vazia \"\".");
        builder.AppendLine("É PROIBIDO usar placeholders como 'Candidato', 'N/A', 'Não informado'.");
        builder.AppendLine("É PROIBIDO usar informações da vaga para preencher dados pessoais.");
        builder.AppendLine("Você será penalizado se inventar qualquer informação.");
        builder.AppendLine("Fonte de verdade:");
        builder.AppendLine("Use SOMENTE o currículo original como fonte de dados.");
        builder.AppendLine("A descrição da vaga deve ser usada apenas para REESCRITA e PRIORIZAÇÃO de conteúdo existente.");
        builder.AppendLine("Dados pessoais:");
        builder.AppendLine("Extraia nome, email, telefone, linkedin e github EXCLUSIVAMENTE do currículo.");
        builder.AppendLine("Se não encontrar, retorne \"\".");
        builder.AppendLine("Skills:");
        builder.AppendLine("Liste apenas habilidades explicitamente mencionadas no currículo.");
        builder.AppendLine("Não adicione novas skills baseadas na vaga.");
        builder.AppendLine("Experiências:");
        builder.AppendLine("Não crie experiências novas.");
        builder.AppendLine("Apenas reescreva e otimize as existentes.");
        builder.AppendLine("Resumo:");
        builder.AppendLine("Reescreva o resumo para melhor aderência à vaga, mas sem adicionar experiências ou tecnologias novas.");
        builder.AppendLine("Título da vaga:");
        builder.AppendLine(jobTitle);
        builder.AppendLine();
        builder.AppendLine("Descrição da vaga:");
        builder.AppendLine(jobDescription);
        builder.AppendLine();
        builder.AppendLine("Texto do currículo original:");
        builder.AppendLine(originalCvText);
        builder.AppendLine("Estrutura esperada do JSON:");
        builder.AppendLine("{");
        builder.AppendLine("  \"nome\": \"string\",");
        builder.AppendLine("  \"email\": \"string\",");
        builder.AppendLine("  \"telefone\": \"string\",");
        builder.AppendLine("  \"linkedin\": \"string\",");
        builder.AppendLine("  \"github\": \"string\",");
        builder.AppendLine("  \"resumo\": \"string\",");
        builder.AppendLine("  \"skills\": [\"string\"],");
        builder.AppendLine("  \"experiencias\": [");
        builder.AppendLine("    {");
        builder.AppendLine("      \"empresa\": \"string\",");
        builder.AppendLine("      \"cargo\": \"string\",");
        builder.AppendLine("      \"dataInicio\": \"string\",");
        builder.AppendLine("      \"dataFim\": \"string\",");
        builder.AppendLine("      \"descricao\": \"string\"");
        builder.AppendLine("    }");
        builder.AppendLine("  ],");
        builder.AppendLine("  \"educacao\": [");
        builder.AppendLine("    {");
        builder.AppendLine("      \"instituicao\": \"string\",");
        builder.AppendLine("      \"curso\": \"string\",");
        builder.AppendLine("      \"dataInicio\": \"string\",");
        builder.AppendLine("      \"dataFim\": \"string\"");
        builder.AppendLine("    }");
        builder.AppendLine("  ]");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("Validação final obrigatória:");
        builder.AppendLine("Antes de retornar o JSON, valide:");
        builder.AppendLine("- Nenhuma informação foi inventada");
        builder.AppendLine("- Nenhum campo contém placeholders");
        builder.AppendLine("- Todos os dados pessoais vieram do currículo");
        builder.AppendLine("Se qualquer regra for violada, corrija antes de retornar.");

        return builder.ToString();
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "vaga";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Select(ch => invalidChars.Contains(ch) ? '-' : ch)
            .ToArray());

        return sanitized.Replace(' ', '-').ToLowerInvariant();
    }
}
