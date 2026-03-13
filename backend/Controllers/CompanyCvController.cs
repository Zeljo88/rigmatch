using System.Text.Json;
using System.Security.Cryptography;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RigMatch.Api.Data;
using RigMatch.Api.Data.Entities;
using RigMatch.Api.Models;
using RigMatch.Api.Services;

namespace RigMatch.Api.Controllers;

[Authorize]
[ApiController]
[Route("company")]
public class CompanyCvController : ControllerBase
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;
    private const int SuggestedAliasPromotionThreshold = 3;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions StorageJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RigMatchDbContext _dbContext;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICvTextExtractionService _cvTextExtractionService;
    private readonly ICvParsingService _cvParsingService;
    private readonly ICvParsingGate _cvParsingGate;
    private readonly ICvDiagnosticsLogger _diagnosticsLogger;
    private readonly IRoleStandardizationService _roleStandardizationService;
    private readonly ILogger<CompanyCvController> _logger;

    public CompanyCvController(
        RigMatchDbContext dbContext,
        IFileStorageService fileStorageService,
        ICvTextExtractionService cvTextExtractionService,
        ICvParsingService cvParsingService,
        ICvParsingGate cvParsingGate,
        ICvDiagnosticsLogger diagnosticsLogger,
        IRoleStandardizationService roleStandardizationService,
        ILogger<CompanyCvController> logger)
    {
        _dbContext = dbContext;
        _fileStorageService = fileStorageService;
        _cvTextExtractionService = cvTextExtractionService;
        _cvParsingService = cvParsingService;
        _cvParsingGate = cvParsingGate;
        _diagnosticsLogger = diagnosticsLogger;
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

        var company = await GetAuthenticatedCompanyAsync(cancellationToken);
        if (company is null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var stagedFilePath = await SaveUploadedFileAsync(file!, cancellationToken);
        try
        {
            var fileHash = await ComputeFileHashAsync(stagedFilePath, cancellationToken);
        await _diagnosticsLogger.LogAsync(
            "upload.received",
            $"companyId={company.Id} fileName={file!.FileName} fileSize={file.Length} fileHash={fileHash}",
            cancellationToken);

        var cachedCandidates = await _dbContext.CvRecords
            .AsNoTracking()
            .Where(record => record.FileHash == fileHash && record.ParsedDraftJson != string.Empty)
            .ToListAsync(cancellationToken);

        var cachedRecord = cachedCandidates
            .OrderByDescending(record => record.UpdatedAtUtc ?? record.CreatedAtUtc)
            .FirstOrDefault();

        if (cachedRecord is not null)
        {
            var cachedProfile = JsonSerializer.Deserialize<ParsedCandidateProfile>(cachedRecord.ParsedDraftJson, JsonOptions);
            if (cachedProfile is not null)
            {
                var storagePath = await _fileStorageService.SaveAsync(
                    stagedFilePath,
                    file.FileName,
                    file.ContentType,
                    cancellationToken);
                await _diagnosticsLogger.LogAsync(
                    "upload.cache-hit",
                    $"sourceRecordId={cachedRecord.Id} fileHash={fileHash}",
                    cancellationToken);

                var cachedUploadRecord = new CvRecord
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    FileUrl = storagePath,
                    FileHash = fileHash,
                    ParsedDraftJson = cachedRecord.ParsedDraftJson,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                };

                _dbContext.CvRecords.Add(cachedUploadRecord);
                await _dbContext.SaveChangesAsync(cancellationToken);

                var cachedDuplicateWarnings = await BuildDuplicateWarningsAsync(
                    company.Id,
                    fileHash,
                    cachedProfile,
                    cachedUploadRecord.Id,
                    cancellationToken);
                await _diagnosticsLogger.LogAsync(
                    "upload.duplicate-warnings",
                    $"recordId={cachedUploadRecord.Id} warningCount={cachedDuplicateWarnings.Count}",
                    cancellationToken);

                return Ok(new CompanyCvUploadResponse(
                    cachedUploadRecord.Id,
                    cachedUploadRecord.FileUrl,
                    cachedProfile,
                    cachedUploadRecord.CreatedAtUtc,
                    cachedDuplicateWarnings));
            }
        }

        string extractedText;
        try
        {
            extractedText = await _cvTextExtractionService.ExtractTextFromPdfAsync(stagedFilePath, cancellationToken);
            await _diagnosticsLogger.LogAsync(
                "upload.extracted",
                $"fileHash={fileHash} extractedChars={extractedText.Length}",
                cancellationToken);
        }
        catch (InvalidDataException)
        {
            return BadRequest(new { message = "The uploaded PDF could not be parsed. Please upload a valid PDF file." });
        }

        ParsedCandidateProfile parsedProfile;
        try
        {
            parsedProfile = await _cvParsingGate.ExecuteAsync(
                token => _cvParsingService.ParseCvTextAsync(extractedText, token),
                cancellationToken);
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

        var persistedStoragePath = await _fileStorageService.SaveAsync(
            stagedFilePath,
            file.FileName,
            file.ContentType,
            cancellationToken);
        var record = new CvRecord
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            FileUrl = persistedStoragePath,
            FileHash = fileHash,
            ParsedDraftJson = JsonSerializer.Serialize(parsedProfile, StorageJsonOptions),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _dbContext.CvRecords.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var duplicateWarnings = await BuildDuplicateWarningsAsync(
            company.Id,
            fileHash,
            parsedProfile,
            record.Id,
            cancellationToken);
        await _diagnosticsLogger.LogAsync(
            "upload.completed",
            $"recordId={record.Id} fileHash={fileHash} warningCount={duplicateWarnings.Count}",
            cancellationToken);

        return Ok(new CompanyCvUploadResponse(
            record.Id,
            record.FileUrl,
            parsedProfile,
            record.CreatedAtUtc,
            duplicateWarnings));
        }
        finally
        {
            DeleteTemporaryFile(stagedFilePath);
        }
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

        var company = await GetAuthenticatedCompanyAsync(cancellationToken);
        if (company is null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var record = await _dbContext.CvRecords
            .FirstOrDefaultAsync(r => r.Id == id && r.CompanyId == company.Id, cancellationToken);

        if (record is null)
        {
            return NotFound(new { message = "CV record not found for this company." });
        }

        record.FinalJson = await NormalizeAndSerializeFinalProfileAsync(request.FinalProfile, cancellationToken);
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await UpsertSuggestedAliasesAsync(company.Id, record.Id, request.FinalProfile, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { id = record.Id, savedAtUtc = record.UpdatedAtUtc });
    }

    [HttpDelete("cv/{id:guid}")]
    public async Task<IActionResult> DeleteCompanyCv(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var company = await GetAuthenticatedCompanyAsync(cancellationToken);
        if (company is null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var record = await _dbContext.CvRecords
            .FirstOrDefaultAsync(r => r.Id == id && r.CompanyId == company.Id, cancellationToken);

        if (record is null)
        {
            return NotFound(new { message = "CV record not found for this company." });
        }

        _dbContext.CvRecords.Remove(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await _fileStorageService.DeleteAsync(record.FileUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete stored file for CV record {CvId}.", record.Id);
        }

        return NoContent();
    }

    [HttpGet("cvs")]
    public async Task<ActionResult<IReadOnlyList<CompanyCvListItem>>> ListCompanyCvs(CancellationToken cancellationToken)
    {
        var company = await GetAuthenticatedCompanyAsync(cancellationToken);
        if (company is null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

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
        var company = await GetAuthenticatedCompanyAsync(cancellationToken);
        if (company is null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }
        var list = await BuildCompanyCvListAsync(company.Id, q, minExp, education, cert, needsReview, cancellationToken);
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
        var company = await GetAuthenticatedCompanyAsync(cancellationToken);
        if (company is null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }
        var record = await _dbContext.CvRecords
            .FirstOrDefaultAsync(r => r.Id == id && r.CompanyId == company.Id, cancellationToken);

        if (record is null)
        {
            return NotFound(new { message = "CV record not found for this company." });
        }

        var structuredJson = record.FinalJson ?? record.ParsedDraftJson;
        var downloadUrl = $"/company/cv/{record.Id}/download";

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
        var company = await GetAuthenticatedCompanyAsync(cancellationToken);
        if (company is null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }
        var record = await _dbContext.CvRecords
            .FirstOrDefaultAsync(r => r.Id == id && r.CompanyId == company.Id, cancellationToken);

        if (record is null)
        {
            return NotFound(new { message = "CV record not found for this company." });
        }

        var stream = await _fileStorageService.OpenReadAsync(record.FileUrl, cancellationToken);
        if (stream is null)
        {
            return NotFound(new { message = "Original CV file was not found on storage." });
        }

        var downloadFileName = Path.GetFileName(record.FileUrl.Replace('/', Path.DirectorySeparatorChar));
        return File(stream, "application/pdf", downloadFileName);
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

    private async Task<Company?> GetAuthenticatedCompanyAsync(CancellationToken cancellationToken)
    {
        var companyIdClaim = User.FindFirstValue("companyId");
        if (!Guid.TryParse(companyIdClaim, out var companyId))
        {
            return null;
        }

        return await _dbContext.Companies
            .FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken);
    }

    private static async Task<string> SaveUploadedFileAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName);
        var storedFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");

        await using (var stream = System.IO.File.Create(storedFilePath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return storedFilePath;
    }

    private static void DeleteTemporaryFile(string filePath)
    {
        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }
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

    private async Task<IReadOnlyList<CompanyCvDuplicateWarning>> BuildDuplicateWarningsAsync(
        Guid companyId,
        string fileHash,
        ParsedCandidateProfile parsedProfile,
        Guid currentRecordId,
        CancellationToken cancellationToken)
    {
        var records = await _dbContext.CvRecords
            .AsNoTracking()
            .Where(record => record.CompanyId == companyId && record.Id != currentRecordId)
            .ToListAsync(cancellationToken);

        var warnings = new List<CompanyCvDuplicateWarning>();

        foreach (var record in records.Where(record => record.FileHash == fileHash))
        {
            warnings.Add(new CompanyCvDuplicateWarning(
                "exact",
                "This exact CV file already exists in your library.",
                record.Id));
        }

        var existingProfiles = records
            .Select(record => new
            {
                record.Id,
                Snapshot = ParseProfileSnapshot(record.FinalJson ?? record.ParsedDraftJson)
            })
            .Where(item => item.Snapshot is not null)
            .ToArray();

        var normalizedEmail = NormalizeIdentityValue(parsedProfile.Email);
        var normalizedPhone = NormalizePhone(parsedProfile.PhoneNumber);
        var normalizedName = NormalizeIdentityValue(parsedProfile.Name);

        foreach (var existing in existingProfiles)
        {
            var snapshot = existing.Snapshot!;
            var existingEmail = NormalizeIdentityValue(snapshot.Email);
            var existingPhone = NormalizePhone(snapshot.PhoneNumber);
            var existingName = NormalizeIdentityValue(snapshot.Name);

            if (!string.IsNullOrWhiteSpace(normalizedEmail) && normalizedEmail == existingEmail)
            {
                warnings.Add(new CompanyCvDuplicateWarning(
                    "probable",
                    $"Possible duplicate candidate: matching email with {snapshot.Name ?? "an existing record"}.",
                    existing.Id));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(normalizedPhone) && normalizedPhone == existingPhone)
            {
                warnings.Add(new CompanyCvDuplicateWarning(
                    "probable",
                    $"Possible duplicate candidate: matching phone number with {snapshot.Name ?? "an existing record"}.",
                    existing.Id));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(normalizedName) && normalizedName == existingName)
            {
                warnings.Add(new CompanyCvDuplicateWarning(
                    "possible",
                    $"Possible duplicate candidate: matching name with {snapshot.Name ?? "an existing record"}.",
                    existing.Id));
            }
        }

        return warnings
            .GroupBy(warning => $"{warning.Type}|{warning.ExistingCvId}|{warning.Message}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static string NormalizeIdentityValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    private static string NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value.Where(char.IsDigit).ToArray());
    }

    private async Task<string> NormalizeAndSerializeFinalProfileAsync(
        JsonElement finalProfile,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<EditableProfilePayload>(finalProfile.GetRawText(), JsonOptions)
                      ?? new EditableProfilePayload();

        var experiences = await NormalizeExperiencesAsync(payload.Experiences, cancellationToken);
        var totalExperienceYears = experiences.Count > 0
            ? RoleExperienceCalculator.CalculateTotalYears(experiences)
            : (int)Math.Round(Math.Max(payload.ExperienceYears ?? 0d, 0d), MidpointRounding.AwayFromZero);

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
            totalExperienceYears,
            experiences,
            RoleExperienceCalculator.Calculate(experiences));

        return JsonSerializer.Serialize(normalized, StorageJsonOptions);
    }

    private async Task UpsertSuggestedAliasesAsync(
        Guid companyId,
        Guid recordId,
        JsonElement finalProfile,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<EditableProfilePayload>(finalProfile.GetRawText(), JsonOptions);
        if (payload?.Experiences is null)
        {
            return;
        }

        foreach (var experience in payload.Experiences)
        {
            if (!(experience.ReviewedByUser))
            {
                continue;
            }

            var rawAlias = (experience.RawRoleTitle ?? experience.Role ?? string.Empty).Trim();
            var standardRoleName = (experience.StandardRoleName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(rawAlias) || string.IsNullOrWhiteSpace(standardRoleName))
            {
                continue;
            }

            var rawAliasNormalized = RoleCatalogSeeder.Normalize(rawAlias);
            var standardRoleNormalized = RoleCatalogSeeder.Normalize(standardRoleName);
            if (rawAliasNormalized.Length == 0 || rawAliasNormalized == standardRoleNormalized)
            {
                continue;
            }

            var standardRole = await _dbContext.StandardRoles
                .FirstOrDefaultAsync(role => role.IsActive && role.Name == standardRoleName, cancellationToken);

            if (standardRole is null)
            {
                continue;
            }

            var existingAlias = await _dbContext.RoleAliases
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    alias => alias.StandardRoleId == standardRole.Id && alias.AliasNormalized == rawAliasNormalized,
                    cancellationToken);

            if (existingAlias is not null)
            {
                continue;
            }

            var suggestedAlias = await _dbContext.SuggestedRoleAliases
                .FirstOrDefaultAsync(
                    alias => alias.CompanyId == companyId &&
                             alias.StandardRoleId == standardRole.Id &&
                             alias.RawAliasNormalized == rawAliasNormalized,
                    cancellationToken);

            var now = DateTimeOffset.UtcNow;
            if (suggestedAlias is null)
            {
                _dbContext.SuggestedRoleAliases.Add(new SuggestedRoleAlias
                {
                    CompanyId = companyId,
                    LastCvRecordId = recordId,
                    StandardRoleId = standardRole.Id,
                    RawAlias = rawAlias,
                    RawAliasNormalized = rawAliasNormalized,
                    ConfirmationCount = 1,
                    FirstSuggestedAtUtc = now,
                    LastSuggestedAtUtc = now
                });
                continue;
            }

            suggestedAlias.RawAlias = rawAlias;
            suggestedAlias.LastCvRecordId = recordId;
            suggestedAlias.LastSuggestedAtUtc = now;
            suggestedAlias.ConfirmationCount += 1;

            if (suggestedAlias.ConfirmationCount < SuggestedAliasPromotionThreshold)
            {
                continue;
            }

            _dbContext.RoleAliases.Add(new RoleAlias
            {
                StandardRoleId = standardRole.Id,
                Alias = rawAlias,
                AliasNormalized = rawAliasNormalized,
                RequiresReview = true
            });

            _dbContext.SuggestedRoleAliases.Remove(suggestedAlias);
        }
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

        return normalizedExperiences
            .Select(exp => !string.IsNullOrWhiteSpace(exp.StandardRoleName) ? exp.StandardRoleName : exp.RawRoleTitle)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
            var match = await _roleStandardizationService.MatchRoleAsync(
                roleToMatch,
                (exp.Description ?? string.Empty).Trim(),
                cancellationToken);
            await _diagnosticsLogger.LogAsync(
                "role.match",
                $"source=save rawRole={rawRole} inputRole={roleToMatch} standardRole={match.StandardRoleName} strategy={match.MatchStrategy} confidence={match.MatchConfidence:0.00} needsReview={match.NeedsReview} details={match.MatchDetails}",
                cancellationToken);

            var standardizedRoleId = match.StandardRoleId;
            var standardizedRoleName = match.StandardRoleName;
            var matchConfidence = match.MatchConfidence;
            var needsReview = match.NeedsReview;
            var reviewedByUser = exp.ReviewedByUser;

            if (!string.IsNullOrWhiteSpace(exp.StandardRoleName) && reviewedByUser)
            {
                var reviewedMatch = await _roleStandardizationService.MatchRoleAsync(exp.StandardRoleName, null, cancellationToken);
                await _diagnosticsLogger.LogAsync(
                    "role.match",
                    $"source=save-reviewed rawRole={rawRole} reviewedRole={exp.StandardRoleName} standardRole={reviewedMatch.StandardRoleName} strategy={reviewedMatch.MatchStrategy} confidence={reviewedMatch.MatchConfidence:0.00} needsReview={reviewedMatch.NeedsReview} details={reviewedMatch.MatchDetails}",
                    cancellationToken);
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

    private static async Task<string> ComputeFileHashAsync(string absolutePath, CancellationToken cancellationToken)
    {
        await using var stream = System.IO.File.OpenRead(absolutePath);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

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
