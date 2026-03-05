namespace RigMatch.Api.Services;

public interface ICvTextExtractionService
{
    Task<string> ExtractTextFromPdfAsync(string pdfPath, CancellationToken cancellationToken = default);
}
