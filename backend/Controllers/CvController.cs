using Microsoft.AspNetCore.Mvc;
using RigMatch.Api.Models;
using RigMatch.Api.Services;

namespace RigMatch.Api.Controllers;

[ApiController]
[Route("cv")]
public class CvController : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".doc",
        ".docx"
    };

    private const long MaxFileSizeBytes = 10 * 1024 * 1024;
    private readonly IWebHostEnvironment _environment;
    private readonly ICvTextExtractionService _cvTextExtractionService;
    private readonly ICvParsingService _cvParsingService;
    private readonly ILogger<CvController> _logger;

    public CvController(
        IWebHostEnvironment environment,
        ICvTextExtractionService cvTextExtractionService,
        ICvParsingService cvParsingService,
        ILogger<CvController> logger)
    {
        _environment = environment;
        _cvTextExtractionService = cvTextExtractionService;
        _cvParsingService = cvParsingService;
        _logger = logger;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFileSizeBytes)]
    public async Task<ActionResult<CvUploadResponse>> Upload([FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        var validationError = ValidateUploadedFile(file);
        if (validationError is not null)
        {
            return validationError;
        }

        var storedFile = await SaveUploadedFileAsync(file!, cancellationToken);

        return Ok(new CvUploadResponse(
            file!.FileName,
            file.Length,
            storedFile.StoragePath,
            DateTimeOffset.UtcNow));
    }

    [HttpPost("extract-text")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFileSizeBytes)]
    public async Task<ActionResult<CvTextExtractionResponse>> ExtractText(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateUploadedFile(file);
        if (validationError is not null)
        {
            return validationError;
        }

        if (!string.Equals(Path.GetExtension(file!.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Day 2 supports text extraction from PDF files only." });
        }

        var storedFile = await SaveUploadedFileAsync(file, cancellationToken);

        try
        {
            var extractedText = await _cvTextExtractionService.ExtractTextFromPdfAsync(storedFile.AbsolutePath, cancellationToken);

            return Ok(new CvTextExtractionResponse(
                file.FileName,
                file.Length,
                storedFile.StoragePath,
                extractedText,
                extractedText.Length,
                DateTimeOffset.UtcNow));
        }
        catch (InvalidDataException)
        {
            return BadRequest(new { message = "The uploaded PDF could not be parsed. Please upload a valid PDF file." });
        }
    }

    [HttpPost("parse")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFileSizeBytes)]
    public async Task<ActionResult<CvParseFileResponse>> Parse(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateUploadedFile(file);
        if (validationError is not null)
        {
            return validationError;
        }

        if (!string.Equals(Path.GetExtension(file!.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Day 3 supports parsing from PDF files only." });
        }

        var storedFile = await SaveUploadedFileAsync(file, cancellationToken);

        string extractedText;
        try
        {
            extractedText = await _cvTextExtractionService.ExtractTextFromPdfAsync(storedFile.AbsolutePath, cancellationToken);
        }
        catch (InvalidDataException)
        {
            return BadRequest(new { message = "The uploaded PDF could not be parsed. Please upload a valid PDF file." });
        }

        ParsedCandidateProfile parsedProfile;
        try
        {
            parsedProfile = await _cvParsingService.ParseCvTextAsync(extractedText, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "CV parsing configuration is invalid.");
            return StatusCode(500, new { message = ex.Message });
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new { message = "Failed to reach the AI parsing service." });
        }

        return Ok(new CvParseFileResponse(
            file.FileName,
            file.Length,
            storedFile.StoragePath,
            extractedText.Length,
            parsedProfile,
            DateTimeOffset.UtcNow));
    }

    [HttpPost("parse-text")]
    public async Task<ActionResult<ParsedCandidateProfile>> ParseText(
        [FromBody] ParseCvTextRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.CvText))
        {
            return BadRequest(new { message = "cvText is required." });
        }

        try
        {
            var parsedProfile = await _cvParsingService.ParseCvTextAsync(request.CvText, cancellationToken);
            return Ok(parsedProfile);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "CV parsing configuration is invalid.");
            return StatusCode(500, new { message = ex.Message });
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new { message = "Failed to reach the AI parsing service." });
        }
    }

    private ActionResult? ValidateUploadedFile(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "A CV file is required." });
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return BadRequest(new { message = "File size exceeds the 10 MB limit." });
        }

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            return BadRequest(new { message = "Only PDF, DOC, and DOCX files are allowed." });
        }

        return null;
    }

    private async Task<StoredFileResult> SaveUploadedFileAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var uploadsDirectory = Path.Combine(_environment.ContentRootPath, "uploads");
        Directory.CreateDirectory(uploadsDirectory);

        var extension = Path.GetExtension(file.FileName);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var storedFilePath = Path.Combine(uploadsDirectory, storedFileName);

        await using (var stream = System.IO.File.Create(storedFilePath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        _logger.LogInformation("CV uploaded. OriginalName={OriginalName} StoredFile={StoredFile}", file.FileName, storedFileName);

        return new StoredFileResult(storedFilePath, $"uploads/{storedFileName}");
    }
}

public sealed record CvUploadResponse(
    string OriginalFileName,
    long SizeBytes,
    string StoragePath,
    DateTimeOffset UploadedAtUtc);

public sealed record CvTextExtractionResponse(
    string OriginalFileName,
    long SizeBytes,
    string StoragePath,
    string ExtractedText,
    int CharacterCount,
    DateTimeOffset ExtractedAtUtc);

internal sealed record StoredFileResult(string AbsolutePath, string StoragePath);
