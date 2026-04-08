namespace jobAgentApi.Application.Abstractions;

public interface IStorageService
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType);
}

public interface IPdfService
{
    Task<string> ExtractTextAsync(Stream pdfStream);
    Task<byte[]> GeneratePdfAsync(string textContent, CancellationToken cancellationToken = default);
}
