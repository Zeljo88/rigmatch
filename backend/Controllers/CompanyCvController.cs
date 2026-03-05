using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RigMatch.Api.Data;
using RigMatch.Api.Data.Entities;
using RigMatch.Api.Models;
using RigMatch.Api.Services;

namespace RigMatch.Api.Controllers;

[ApiController]
[Route("company")]
public class CompanyCvController : ControllerBase
{
    private const string CompanyHeaderName = "X-Company-Id";
    private const string DefaultCompanyId = "rigmatch-demo-company";
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly RigMatchDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;
    private readonly ICvTextExtractionService _cvTextExtractionService;
    private readonly ICvParsingService _cvParsingService;
    private readonly ILogger<CompanyCvController> _logger;

    public CompanyCvController(
        RigMatchDbContext dbContext,
        IWebHostEnvironment environment,
        ICvTextExtractionService cvTextExtractionService,
        ICvParsingService cvParsingService,
        ILogger<CompanyCvController> logger)
    {
        _dbContext = dbContext;
        _environment = environment;
        _cvTextExtractionService = cvTextExtractionService;
        _cvParsingService = cvParsingService;
        _logger = logger;
    }

    [HttpPost("cv/upload")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFileSizeBytes)]
    public async Task<ActionResult<CompanyCvUploadResponse>> UploadCompanyCv(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        var validationError = ValidatePdfFile(file);
        if (validationError is not null)
        {
            return validationError;
        }

        var company = await GetOrCreateCompanyAsync(cancellationToken);
        var storedFile = await SaveUploadedFileAsync(file!, cancellationToken);

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
        catch (AiServiceException ex) when (ex.StatusCode == 429)
        {
            return StatusCode(429, new { message = ex.Message, retryAfterSeconds = ex.RetryAfterSeconds });
        }
        catch (AiServiceException ex)
        {
            return StatusCode(502, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "CV parsing configuration is invalid.");
            return StatusCode(500, new { message = ex.Message });
        }

        var record = new CvRecord
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            FileUrl = storedFile.StoragePath,
            ParsedDraftJson = JsonSerializer.Serialize(parsedProfile),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _dbContext.CvRecords.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new CompanyCvUploadResponse(
            record.Id,
            record.FileUrl,
            parsedProfile,
            record.CreatedAtUtc));
    }

    [HttpPost("cv/{id:guid}/save")]
    public async Task<IActionResult> SaveCompanyCv(
        [FromRoute] Guid id,
        [FromBody] SaveCompanyCvRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null || request.FinalProfile.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return BadRequest(new { message = "finalProfile is required." });
        }

        var company = await GetOrCreateCompanyAsync(cancellationToken);

        var record = await _dbContext.CvRecords
            .FirstOrDefaultAsync(r => r.Id == id && r.CompanyId == company.Id, cancellationToken);

        if (record is null)
        {
            return NotFound(new { message = "CV record not found for this company." });
        }

        record.FinalJson = request.FinalProfile.GetRawText();
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { id = record.Id, savedAtUtc = record.UpdatedAtUtc });
    }

    [HttpGet("cvs")]
    public async Task<ActionResult<IReadOnlyList<CompanyCvListItem>>> ListCompanyCvs(CancellationToken cancellationToken)
    {
        var company = await GetOrCreateCompanyAsync(cancellationToken);

        var list = await BuildCompanyCvListAsync(company.Id, null, null, null, null, cancellationToken);

        return Ok(list);
    }

    [HttpGet("cvs/search")]
    public async Task<ActionResult<IReadOnlyList<CompanyCvListItem>>> SearchCompanyCvs(
        [FromQuery] string? q,
        [FromQuery] int? minExp,
        [FromQuery] string? location,
        [FromQuery] string? cert,
        CancellationToken cancellationToken)
    {
        var company = await GetOrCreateCompanyAsync(cancellationToken);
        var list = await BuildCompanyCvListAsync(company.Id, q, minExp, location, cert, cancellationToken);
        return Ok(list);
    }

    [HttpGet("cvs/{id:guid}")]
    public async Task<ActionResult<CompanyCvDetailResponse>> GetCompanyCvDetails(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var company = await GetOrCreateCompanyAsync(cancellationToken);
        var record = await _dbContext.CvRecords
            .FirstOrDefaultAsync(r => r.Id == id && r.CompanyId == company.Id, cancellationToken);

        if (record is null)
        {
            return NotFound(new { message = "CV record not found for this company." });
        }

        var structuredJson = record.FinalJson ?? record.ParsedDraftJson;
        var downloadUrl = $"/company/cv/{record.Id}/download?companyId={Uri.EscapeDataString(company.ExternalId)}";

        return Ok(new CompanyCvDetailResponse(
            record.Id,
            record.FileUrl,
            structuredJson,
            !string.IsNullOrWhiteSpace(record.FinalJson),
            record.CreatedAtUtc,
            record.UpdatedAtUtc,
            downloadUrl));
    }

    [HttpGet("cv/{id:guid}/download")]
    public async Task<IActionResult> DownloadCompanyCv(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var company = await GetOrCreateCompanyAsync(cancellationToken);
        var record = await _dbContext.CvRecords
            .FirstOrDefaultAsync(r => r.Id == id && r.CompanyId == company.Id, cancellationToken);

        if (record is null)
        {
            return NotFound(new { message = "CV record not found for this company." });
        }

        var normalizedRelativePath = record.FileUrl.Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = Path.Combine(_environment.ContentRootPath, normalizedRelativePath);

        if (!System.IO.File.Exists(absolutePath))
        {
            return NotFound(new { message = "Original CV file was not found on storage." });
        }

        var downloadFileName = Path.GetFileName(absolutePath);
        return PhysicalFile(absolutePath, "application/pdf", downloadFileName);
    }

    private ActionResult? ValidatePdfFile(IFormFile? file)
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
        if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Company CV upload supports PDF files only." });
        }

        return null;
    }

    private async Task<Company> GetOrCreateCompanyAsync(CancellationToken cancellationToken)
    {
        var externalId = Request.Headers[CompanyHeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(externalId))
        {
            externalId = Request.Query["companyId"].FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(externalId))
        {
            externalId = DefaultCompanyId;
        }

        var company = await _dbContext.Companies
            .FirstOrDefaultAsync(c => c.ExternalId == externalId, cancellationToken);

        if (company is not null)
        {
            return company;
        }

        company = new Company
        {
            Id = Guid.NewGuid(),
            ExternalId = externalId,
            Name = externalId,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _dbContext.Companies.Add(company);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return company;
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

        return new StoredFileResult(storedFilePath, $"uploads/{storedFileName}");
    }

    private static ProfileSnapshot? ParseProfileSnapshot(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ProfileSnapshot>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<CompanyCvListItem>> BuildCompanyCvListAsync(
        Guid companyId,
        string? q,
        int? minExp,
        string? location,
        string? cert,
        CancellationToken cancellationToken)
    {
        var records = await _dbContext.CvRecords
            .Where(r => r.CompanyId == companyId)
            .ToListAsync(cancellationToken);

        var rows = records.Select(record =>
        {
            var snapshot = ParseProfileSnapshot(record.FinalJson ?? record.ParsedDraftJson);
            return new CvRow(record, snapshot);
        });

        if (!string.IsNullOrWhiteSpace(q))
        {
            var needle = q.Trim();
            rows = rows.Where(row =>
                Contains(row.Snapshot?.Name, needle) ||
                ContainsAny(row.Snapshot?.Skills, needle) ||
                ContainsAny(row.Snapshot?.Certifications, needle) ||
                ContainsAny(row.Snapshot?.JobTitles, needle) ||
                ContainsAny(row.Snapshot?.Companies, needle));
        }

        if (minExp.HasValue)
        {
            rows = rows.Where(row => (row.Snapshot?.ExperienceYears ?? 0) >= minExp.Value);
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            var locationNeedle = location.Trim();
            rows = rows.Where(row => Contains(row.Snapshot?.Location, locationNeedle));
        }

        if (!string.IsNullOrWhiteSpace(cert))
        {
            var certNeedle = cert.Trim();
            rows = rows.Where(row => ContainsAny(row.Snapshot?.Certifications, certNeedle));
        }

        return rows
            .OrderByDescending(row => row.Record.CreatedAtUtc)
            .Select(row => new CompanyCvListItem(
                row.Record.Id,
                row.Snapshot?.Name ?? "Unknown candidate",
                row.Snapshot?.JobTitles?.FirstOrDefault() ?? "N/A",
                row.Snapshot?.Location,
                row.Snapshot?.ExperienceYears,
                row.Record.CreatedAtUtc,
                !string.IsNullOrWhiteSpace(row.Record.FinalJson)))
            .ToArray();
    }

    private static bool Contains(string? value, string search)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAny(IEnumerable<string>? values, string search)
    {
        return values?.Any(v => Contains(v, search)) ?? false;
    }

    private sealed record StoredFileResult(string AbsolutePath, string StoragePath);

    private sealed class ProfileSnapshot
    {
        public string? Name { get; set; }

        public string? Email { get; set; }

        public IReadOnlyList<string>? JobTitles { get; set; }

        public IReadOnlyList<string>? Companies { get; set; }

        public IReadOnlyList<string>? Skills { get; set; }

        public IReadOnlyList<string>? Certifications { get; set; }

        public string? Location { get; set; }

        public int? ExperienceYears { get; set; }
    }

    private sealed record CvRow(CvRecord Record, ProfileSnapshot? Snapshot);
}
