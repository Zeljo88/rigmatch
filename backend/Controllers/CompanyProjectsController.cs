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
public class CompanyProjectsController : ControllerBase
{
    private const string CompanyHeaderName = "X-Company-Id";
    private const string DefaultCompanyId = "rigmatch-demo-company";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RigMatchDbContext _dbContext;
    private readonly IProjectMatchingService _projectMatchingService;

    public CompanyProjectsController(
        RigMatchDbContext dbContext,
        IProjectMatchingService projectMatchingService)
    {
        _dbContext = dbContext;
        _projectMatchingService = projectMatchingService;
    }

    [HttpGet("projects")]
    public async Task<ActionResult<IReadOnlyList<CompanyProjectListItem>>> ListProjects(CancellationToken cancellationToken)
    {
        var company = await GetOrCreateCompanyAsync(cancellationToken);
        var projects = await _dbContext.CompanyProjects
            .Where(project => project.CompanyId == company.Id)
            .ToListAsync(cancellationToken);

        var list = projects
            .OrderByDescending(project => project.UpdatedAtUtc ?? project.CreatedAtUtc)
            .Select(project => new CompanyProjectListItem(
                project.Id,
                project.Title,
                project.PrimaryRole,
                project.Location,
                project.Status,
                project.CreatedAtUtc,
                project.UpdatedAtUtc))
            .ToArray();

        return Ok(list);
    }

    [HttpPost("projects")]
    public async Task<ActionResult<CompanyProjectDetailResponse>> CreateProject(
        [FromBody] SaveCompanyProjectRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { message = "Project payload is required." });
        }

        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return validationError;
        }

        var company = await GetOrCreateCompanyAsync(cancellationToken);
        var project = BuildProjectEntity(request, company.Id);
        project.Id = Guid.NewGuid();
        project.CreatedAtUtc = DateTimeOffset.UtcNow;

        _dbContext.CompanyProjects.Add(project);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await BuildProjectDetailResponseAsync(project, cancellationToken);
        return CreatedAtAction(nameof(GetProject), new { id = project.Id }, response);
    }

    [HttpPut("projects/{id:guid}")]
    public async Task<ActionResult<CompanyProjectDetailResponse>> UpdateProject(
        [FromRoute] Guid id,
        [FromBody] SaveCompanyProjectRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { message = "Project payload is required." });
        }

        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return validationError;
        }

        var company = await GetOrCreateCompanyAsync(cancellationToken);
        var project = await _dbContext.CompanyProjects
            .FirstOrDefaultAsync(item => item.Id == id && item.CompanyId == company.Id, cancellationToken);

        if (project is null)
        {
            return NotFound(new { message = "Project not found for this company." });
        }

        ApplyRequest(project, request);
        project.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(await BuildProjectDetailResponseAsync(project, cancellationToken));
    }

    [HttpDelete("projects/{id:guid}")]
    public async Task<IActionResult> DeleteProject(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var company = await GetOrCreateCompanyAsync(cancellationToken);
        var project = await _dbContext.CompanyProjects
            .FirstOrDefaultAsync(item => item.Id == id && item.CompanyId == company.Id, cancellationToken);

        if (project is null)
        {
            return NotFound(new { message = "Project not found for this company." });
        }

        _dbContext.CompanyProjects.Remove(project);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("projects/{id:guid}")]
    public async Task<ActionResult<CompanyProjectDetailResponse>> GetProject(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var company = await GetOrCreateCompanyAsync(cancellationToken);
        var project = await _dbContext.CompanyProjects
            .FirstOrDefaultAsync(item => item.Id == id && item.CompanyId == company.Id, cancellationToken);

        if (project is null)
        {
            return NotFound(new { message = "Project not found for this company." });
        }

        return Ok(await BuildProjectDetailResponseAsync(project, cancellationToken));
    }

    private async Task<CompanyProjectDetailResponse> BuildProjectDetailResponseAsync(
        CompanyProject project,
        CancellationToken cancellationToken)
    {
        var finalizedCandidates = await _dbContext.CvRecords
            .Where(record => record.CompanyId == project.CompanyId && record.FinalJson != null && record.FinalJson != string.Empty)
            .ToArrayAsync(cancellationToken);

        var matches = _projectMatchingService.MatchCandidates(project, finalizedCandidates);

        return new CompanyProjectDetailResponse(
            project.Id,
            project.Title,
            project.ClientName,
            project.PrimaryRole,
            DeserializeList(project.AdditionalRolesJson),
            DeserializeList(project.RequiredSkillsJson),
            DeserializeList(project.PreferredSkillsJson),
            DeserializeList(project.RequiredCertificationsJson),
            DeserializeList(project.PreferredCertificationsJson),
            project.MinimumExperienceYears,
            project.Location,
            project.PreferredEducation,
            project.Description,
            project.Status,
            project.StartDateUtc,
            project.CreatedAtUtc,
            project.UpdatedAtUtc,
            matches);
    }

    private static CompanyProject BuildProjectEntity(SaveCompanyProjectRequest request, Guid companyId)
    {
        var project = new CompanyProject
        {
            CompanyId = companyId
        };

        ApplyRequest(project, request);
        return project;
    }

    private static void ApplyRequest(CompanyProject project, SaveCompanyProjectRequest request)
    {
        project.Title = request.Title!.Trim();
        project.ClientName = (request.ClientName ?? string.Empty).Trim();
        project.PrimaryRole = request.PrimaryRole!.Trim();
        project.AdditionalRolesJson = SerializeList(request.AdditionalRoles);
        project.RequiredSkillsJson = SerializeList(request.RequiredSkills);
        project.PreferredSkillsJson = SerializeList(request.PreferredSkills);
        project.RequiredCertificationsJson = SerializeList(request.RequiredCertifications);
        project.PreferredCertificationsJson = SerializeList(request.PreferredCertifications);
        project.MinimumExperienceYears = request.MinimumExperienceYears;
        project.Location = (request.Location ?? string.Empty).Trim();
        project.PreferredEducation = (request.PreferredEducation ?? string.Empty).Trim();
        project.Description = (request.Description ?? string.Empty).Trim();
        project.Status = string.IsNullOrWhiteSpace(request.Status) ? "Draft" : request.Status.Trim();
        project.StartDateUtc = request.StartDateUtc;
    }

    private ActionResult? ValidateRequest(SaveCompanyProjectRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new { message = "Project title is required." });
        }

        if (string.IsNullOrWhiteSpace(request.PrimaryRole))
        {
            return BadRequest(new { message = "Primary standard role is required." });
        }

        if (request.MinimumExperienceYears is < 0)
        {
            return BadRequest(new { message = "Minimum experience years cannot be negative." });
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

    private static string SerializeList(IEnumerable<string>? values)
    {
        var items = values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];

        return JsonSerializer.Serialize(items, JsonOptions);
    }

    private static IReadOnlyList<string> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<string>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
