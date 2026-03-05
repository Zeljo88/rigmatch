using System.Text;
using UglyToad.PdfPig;

namespace RigMatch.Api.Services;

public class CvTextExtractionService : ICvTextExtractionService
{
    public Task<string> ExtractTextFromPdfAsync(string pdfPath, CancellationToken cancellationToken = default)
    {
        if (!System.IO.File.Exists(pdfPath))
        {
            throw new FileNotFoundException("The requested PDF file was not found.", pdfPath);
        }

        try
        {
            var sb = new StringBuilder();

            using var document = PdfDocument.Open(pdfPath);
            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                sb.AppendLine(page.Text);
            }

            return Task.FromResult(sb.ToString().Trim());
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or ArgumentException)
        {
            throw new InvalidDataException("Could not extract text from the supplied PDF.", ex);
        }
    }
}
