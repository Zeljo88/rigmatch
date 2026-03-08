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

    private static readonly JsonSerializerOptions StorageJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RigMatchDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;
    private readonly ICvTextExtractionService _cvTextExtractionService;
    private readonly ICvParsingService _cvParsingService;
    private readonly IRoleStandardizationService _roleStandardizationService;
    private readonly ILogger<CompanyCvController> _logger;

    public CompanyCvController(
        RigMatchDbContext dbContext,
        IWebHostEnvironment environment,
        ICvTextExtractionService cvTextExtractionService,
        ICvParsingService cvParsingService,
        IRoleStandardizationService roleStandardizationService,
        ILogger<CompanyCvController> logger)
    {
        _dbContext = dbContext;
        _environment = environment;
        _cvTextExtractionService = cvTextExtractionService;
        _cvParsingService = cvParsingService;
        _roleStandardizationService = roleStandardizationService;
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
            ParsedDraftJson = JsonSerializer.Serialize(parsedProfile, StorageJsonOptions),
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

        record.FinalJson = await NormalizeAndSerializeFinalProfileAsync(request.FinalProfile, cancellationToken);
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { id = record.Id, savedAtUtc = record.UpdatedAtUtc });
    }

    [HttpDelete("cv/{id:guid}")]
    public async Task<IActionResult> DeleteCompanyCv(
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

        _dbContext.CvRecords.Remove(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (System.IO.File.Exists(absolutePath))
        {
            try
            {
                System.IO.File.Delete(absolutePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete stored file for CV record {CvId}.", record.Id);
            }
        }

        return NoContent();
    }

    [HttpGet("cvs")]
    public async Task<ActionResult<IReadOnlyList<CompanyCvListItem>>> ListCompanyCvs(CancellationToken cancellationToken)
    {
        var company = await GetOrCreateCompanyAsync(cancellationToken);

        var list = await BuildCompanyCvListAsync(company.Id, null, null, null, null, null, cancellationToken);

        return Ok(list);
    }

    [HttpGet("cvs/search")]
    public async Task<ActionResult<IReadOnlyList<CompanyCvListItem>>> SearchCompanyCvs(
        [FromQuery] string? q,
        [FromQuery] int? minExp,
        [FromQuery] string? education,
        [FromQuery] string? location,
        [FromQuery] string? cert,
        [FromQuery] bool? needsReview,
        CancellationToken cancellationToken)
    {
        var company = await GetOrCreateCompanyAsync(cancellationToken);
        var educationFilter = !string.IsNullOrWhiteSpace(education) ? education : location;
        var list = await BuildCompanyCvListAsync(company.Id, q, minExp, educationFilter, cert, needsReview, cancellationToken);
        return Ok(list);
    }

    [HttpGet("roles/standard")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetStandardRoles(CancellationToken cancellationToken)
    {
        var roles = await _roleStandardizationService.GetStandardRolesAsync(cancellationToken);
        return Ok(roles);
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
        string? education,
        string? cert,
        bool? needsReview,
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

        if (!string.IsNullOrWhiteSpace(education))
        {
            var educationNeedle = education.Trim();
            rows = rows.Where(row => Contains(row.Snapshot?.HighestEducation, educationNeedle));
        }

        if (!string.IsNullOrWhiteSpace(cert))
        {
            var certNeedle = cert.Trim();
            rows = rows.Where(row => ContainsAny(row.Snapshot?.Certifications, certNeedle));
        }

        if (needsReview.HasValue)
        {
            rows = needsReview.Value
                ? rows.Where(row => row.Snapshot?.Experiences?.Any(exp => exp.NeedsReview) ?? false)
                : rows.Where(row => !(row.Snapshot?.Experiences?.Any(exp => exp.NeedsReview) ?? false));
        }

        return rows
            .OrderByDescending(row => row.Record.CreatedAtUtc)
            .Select(row => new CompanyCvListItem(
                row.Record.Id,
                row.Snapshot?.Name ?? "Unknown candidate",
                row.Snapshot?.JobTitles?.FirstOrDefault() ?? "N/A",
                row.Snapshot?.HighestEducation,
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

    private async Task<string> NormalizeAndSerializeFinalProfileAsync(
        JsonElement finalProfile,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<EditableProfilePayload>(finalProfile.GetRawText(), JsonOptions)
                      ?? new EditableProfilePayload();

        var experiences = await NormalizeExperiencesAsync(payload.Experiences, cancellationToken);

        var normalized = new NormalizedProfilePayload(
            (payload.Name ?? string.Empty).Trim(),
            (payload.Email ?? string.Empty).Trim(),
            (payload.PhoneNumber ?? string.Empty).Trim(),
            (payload.HighestEducation ?? string.Empty).Trim(),
            (payload.Location ?? string.Empty).Trim(),
            await NormalizeRolesAsync(payload.JobTitles, experiences, cancellationToken),
            NormalizeList(payload.Companies),
            NormalizeList(payload.Skills),
            NormalizeList(payload.Certifications),
            (int)Math.Round(Math.Max(payload.ExperienceYears ?? 0d, 0d), MidpointRounding.AwayFromZero),
            experiences,
            RoleExperienceCalculator.Calculate(experiences));

        return JsonSerializer.Serialize(normalized, StorageJsonOptions);
    }

    private async Task<IReadOnlyList<string>> NormalizeRolesAsync(
        IEnumerable<string>? rolesFromPayload,
        IReadOnlyList<ParsedExperienceEntry> normalizedExperiences,
        CancellationToken cancellationToken)
    {
        var direct = await _roleStandardizationService.StandardizeRoleListAsync(rolesFromPayload, cancellationToken);
        if (direct.Count > 0)
        {
            return direct;
        }

        return await _roleStandardizationService.StandardizeRoleListAsync(
            normalizedExperiences.Select(exp => exp.StandardRoleName),
            cancellationToken);
    }

    private async Task<IReadOnlyList<ParsedExperienceEntry>> NormalizeExperiencesAsync(
        IEnumerable<EditableExperiencePayload>? experiences,
        CancellationToken cancellationToken)
    {
        if (experiences is null)
        {
            return [];
        }

        var normalized = new List<ParsedExperienceEntry>();
        foreach (var exp in experiences)
        {
            var rawRole = (exp.RawRoleTitle ?? exp.Role ?? string.Empty).Trim();
            var roleToMatch = (exp.StandardRoleName ?? exp.Role ?? rawRole).Trim();
            var match = await _roleStandardizationService.MatchRoleAsync(roleToMatch, cancellationToken);

            var standardizedRoleId = match.StandardRoleId;
            var standardizedRoleName = match.StandardRoleName;
            var matchConfidence = match.MatchConfidence;
            var needsReview = match.NeedsReview;
            var reviewedByUser = exp.ReviewedByUser;

            if (!string.IsNullOrWhiteSpace(exp.StandardRoleName) && reviewedByUser)
            {
                var reviewedMatch = await _roleStandardizationService.MatchRoleAsync(exp.StandardRoleName, cancellationToken);
                standardizedRoleId = reviewedMatch.StandardRoleId;
                standardizedRoleName = reviewedMatch.StandardRoleName;
                matchConfidence = Math.Max(reviewedMatch.MatchConfidence, 0.99d);
                needsReview = false;
            }

            var item = new ParsedExperienceEntry(
                (exp.CompanyName ?? string.Empty).Trim(),
                rawRole,
                standardizedRoleId,
                standardizedRoleName,
                matchConfidence,
                needsReview,
                reviewedByUser,
                (exp.StartDate ?? string.Empty).Trim(),
                (exp.EndDate ?? string.Empty).Trim(),
                (exp.Description ?? string.Empty).Trim());

            if (string.IsNullOrWhiteSpace(item.CompanyName) &&
                string.IsNullOrWhiteSpace(item.StandardRoleName) &&
                string.IsNullOrWhiteSpace(item.RawRoleTitle) &&
                string.IsNullOrWhiteSpace(item.Description))
            {
                continue;
            }

            normalized.Add(item);
        }

        return normalized;
    }

    private static IReadOnlyList<string> NormalizeList(IEnumerable<string>? items)
    {
        return items?
                   .Where(item => !string.IsNullOrWhiteSpace(item))
                   .Select(item => item.Trim())
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .ToArray()
               ?? [];
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

        public string? PhoneNumber { get; set; }

        public string? HighestEducation { get; set; }

        public int? ExperienceYears { get; set; }

        public IReadOnlyList<ProfileSnapshotExperience>? Experiences { get; set; }
    }

    private sealed class ProfileSnapshotExperience
    {
        public bool NeedsReview { get; set; }
    }

    private sealed record CvRow(CvRecord Record, ProfileSnapshot? Snapshot);

    private sealed class EditableProfilePayload
    {
        public string? Name { get; set; }

        public string? Email { get; set; }

        public string? PhoneNumber { get; set; }

        public string? Location { get; set; }

        public string? HighestEducation { get; set; }

        public IReadOnlyList<string>? JobTitles { get; set; }

        public IReadOnlyList<string>? Companies { get; set; }

        public IReadOnlyList<string>? Skills { get; set; }

        public IReadOnlyList<string>? Certifications { get; set; }

        public double? ExperienceYears { get; set; }

        public IReadOnlyList<EditableExperiencePayload>? Experiences { get; set; }
    }

    private sealed class EditableExperiencePayload
    {
        public string? CompanyName { get; set; }

        public string? RawRoleTitle { get; set; }

        public int? StandardRoleId { get; set; }

        public string? StandardRoleName { get; set; }

        public double? MatchConfidence { get; set; }

        public bool NeedsReview { get; set; }

        public bool ReviewedByUser { get; set; }

        public string? Role { get; set; }

        public string? StartDate { get; set; }

        public string? EndDate { get; set; }

        public string? Description { get; set; }
    }

    private sealed record NormalizedProfilePayload(
        string Name,
        string Email,
        string PhoneNumber,
        string HighestEducation,
        string Location,
        IReadOnlyList<string> JobTitles,
        IReadOnlyList<string> Companies,
        IReadOnlyList<string> Skills,
        IReadOnlyList<string> Certifications,
        int ExperienceYears,
        IReadOnlyList<ParsedExperienceEntry> Experiences,
        IReadOnlyList<RoleExperienceBreakdownItem> RoleExperience);
}
