using System.Text;
using jobAgentApi.Application.Abstractions;
using jobAgentApi.Application.Abstractions.Messaging;
using jobAgentApi.Application.Repositories;
using jobAgentApi.Domain.Entities;

namespace jobAgentApi.Application.Features.User.Commands.UploadCv;

public sealed class UploadCvCommandHandler : ICommandHandler<UploadCvCommand, string>
{
    private readonly IStorageService _storageService;
    private readonly IPdfService _pdfService;
    private readonly ICvAiService _cvAiService;
    private readonly IUnitOfWork _unitOfWork;

    public UploadCvCommandHandler(
        IStorageService storageService,
        IPdfService pdfService,
        ICvAiService cvAiService,
        IUnitOfWork unitOfWork)
    {
        _storageService = storageService;
        _pdfService = pdfService;
        _cvAiService = cvAiService;
        _unitOfWork = unitOfWork;
    }

    public async Task<string> Handle(UploadCvCommand request, CancellationToken cancellationToken)
    {
        var streamToProcess = request.FileStream;

        if (!request.FileStream.CanSeek)
        {
            var bufferedStream = new MemoryStream();
            await request.FileStream.CopyToAsync(bufferedStream, cancellationToken);
            bufferedStream.Position = 0;
            streamToProcess = bufferedStream;
        }

        try
        {
            // 1. Extrair texto do PDF para processamentos futuros
            var extractedText = await _pdfService.ExtractTextAsync(streamToProcess);

            var aiPrompt = BuildCvNormalizationPrompt(extractedText);
            var normalizedText = await _cvAiService.GenerateTailoredCvAsync(aiPrompt, cancellationToken);

            if (string.IsNullOrWhiteSpace(normalizedText))
            {
                throw new DomainException("A IA retornou um texto vazio para o currículo.", 502);
            }

            if (streamToProcess.CanSeek)
            {
                streamToProcess.Position = 0;
            }

            // 2. Upload para o Storage (Supabase)
            var folder = $"cvs/{request.UserId}";
            var fileName = $"{request.FileName}";
            var path = $"{folder}/{fileName}";

            var fileUrl = await _storageService.UploadFileAsync(streamToProcess, path, request.ContentType);

            // 3. Salvar registro no banco de dados
            var userCvRepository = _unitOfWork.GetRepository<UserCv>();

            var usersCvs = await userCvRepository.GetAllAsync();
            var userCV = usersCvs.FirstOrDefault(cv => cv.UserId == request.UserId);
            if (userCV != null)
            {
                userCV.UrlFile = fileUrl;
                userCV.ExtractedText = normalizedText;
                userCV.LastModifiedAt = DateTime.UtcNow;
                userCV.LastModifiedBy = request.UserId.ToString();
                await userCvRepository.UpdateAsync(userCV);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return fileUrl;
            }

            var userCv = new UserCv
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                UrlFile = fileUrl,
                ExtractedText = normalizedText,
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow,
                Active = true,
                LastModifiedBy = request.UserId.ToString(),
                CreatedBy = request.UserId.ToString()
            };

            await userCvRepository.AddAsync(userCv);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return fileUrl;
        }
        finally
        {
            if (!ReferenceEquals(streamToProcess, request.FileStream))
            {
                await streamToProcess.DisposeAsync();
            }
        }
    }

    private static string BuildCvNormalizationPrompt(string extractedText)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Você é um especialista em recrutamento e análise de currículos.");
        builder.AppendLine("Sua tarefa é transformar o currículo abaixo em um JSON estruturado.");
        builder.AppendLine();

        builder.AppendLine("Regras importantes:");
        builder.AppendLine("- Retorne APENAS um JSON válido");
        builder.AppendLine("- Não adicione explicações");
        builder.AppendLine("- Não use markdown");
        builder.AppendLine("- Se algum campo não existir, use null");
        builder.AppendLine("- Não diminua o conteúdo do currículo, seja o mais completo possível");
        builder.AppendLine();

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

        builder.AppendLine("Currículo:");
        builder.AppendLine("```");
        builder.AppendLine(extractedText);
        builder.AppendLine("```");       

        return builder.ToString();
    }
}
