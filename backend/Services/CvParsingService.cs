using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using RigMatch.Api.Models;

namespace RigMatch.Api.Services;

public sealed class CvParsingService : ICvParsingService
{
    private const int DefaultSectionWindowSize = 10;
    private const int MaxRepeatedShortLineOccurrences = 2;
    private const int MaxFormatRetryAttempts = 2;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly string[] KnownSkills =
    [
        "C#",
        ".NET",
        "ASP.NET",
        "Angular",
        "TypeScript",
        "JavaScript",
        "SQL",
        "PostgreSQL",
        "Azure",
        "Docker",
        "Kubernetes",
        "Python",
        "Java",
        "React",
        "Node.js"
    ];

    private readonly HttpClient _httpClient;
    private readonly IWebHostEnvironment _environment;
    private readonly IRoleStandardizationService _roleStandardizationService;
    private readonly IParsingReferenceService _parsingReferenceService;
    private readonly ICvDiagnosticsLogger _diagnosticsLogger;
    private readonly IOptions<CvParsingOptions> _options;
    private readonly ILogger<CvParsingService> _logger;
    private const int MaxAttempts = 3;
    private static readonly string[] RelevantParagraphMarkers =
    [
        "summary",
        "profile",
        "about",
        "experience",
        "employment",
        "work history",
        "education",
        "skills",
        "certification",
        "certificate",
        "contact",
        "phone",
        "email",
        "linkedin"
    ];
    private static readonly string[] SectionHeadings =
    [
        "experience",
        "employment",
        "work history",
        "professional experience",
        "career history",
        "education",
        "skills",
        "certification",
        "certifications"
    ];

    public CvParsingService(
        HttpClient httpClient,
        IWebHostEnvironment environment,
        IRoleStandardizationService roleStandardizationService,
        IParsingReferenceService parsingReferenceService,
        ICvDiagnosticsLogger diagnosticsLogger,
        IOptions<CvParsingOptions> options,
        ILogger<CvParsingService> logger)
    {
        _httpClient = httpClient;
        _environment = environment;
        _roleStandardizationService = roleStandardizationService;
        _parsingReferenceService = parsingReferenceService;
        _diagnosticsLogger = diagnosticsLogger;
        _options = options;
        _logger = logger;
    }

    public async Task<ParsedCandidateProfile> ParseCvTextAsync(string cvText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cvText))
        {
            throw new ArgumentException("CV text cannot be empty.", nameof(cvText));
        }

        var options = _options.Value;
        var normalizedText = PrepareCvText(cvText, options.MaxTextChars);
        var referenceBlock = await _parsingReferenceService.BuildPromptReferenceBlockAsync(normalizedText, cancellationToken);
        await _diagnosticsLogger.LogAsync(
            "parser.prepare",
            $"rawChars={cvText.Length} preparedChars={normalizedText.Length} referenceChars={referenceBlock.Length} maxTextChars={options.MaxTextChars} maxCompletionTokens={options.MaxCompletionTokens}",
            cancellationToken);

        if (_environment.IsDevelopment() && options.UseMockInDevelopment)
        {
            _logger.LogInformation("Using mock CV parsing in Development environment.");
            await _diagnosticsLogger.LogAsync("parser.mock", "development mock parsing enabled", cancellationToken);
            return BuildMockProfile(normalizedText);
        }

        ValidateOptions(options);

        var requestPayload = new
        {
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = """
                              You are RigMatch CV Parser v1.
                              - Output MUST be valid JSON only. No markdown. No explanations.
                              - Do NOT guess or invent. If a field is not explicit, return null or [].
                              - Prefer extraction over summarization.
                              - Normalize dates to "YYYY-MM", "YYYY", "Present", or null.
                              - Remove duplicates in arrays (case-insensitive).
                              - Extract every clearly identifiable work experience entry from the CV, up to 12 experiences.
                              - Keep skills <= 25, certifications <= 15.
                              - Keep each experience description short plain text, max 60 chars.
                              - endDate must be a single value, never a range.
                              - If a title is uncommon, still return the original role text in experiences.role.
                              """
                },
                new
                {
                    role = "user",
                    content = $$"""
                                Extract structured data from this CV text and return JSON exactly matching this schema:
                                {
                                  "name": "string|null",
                                  "email": "string|null",
                                  "phoneNumber": "string|null",
                                  "highestEducation": "string|null",
                                  "skills": ["string"],
                                  "certifications": ["string"],
                                  "experienceYears": "number|null",
                                  "experiences": [
                                    {
                                      "companyName": "string|null",
                                      "role": "string|null",
                                      "startDate": "YYYY-MM|YYYY|null",
                                      "endDate": "YYYY-MM|YYYY|Present|null",
                                      "description": "string|null"
                                    }
                                  ],
                                }
                                
                                {{(string.IsNullOrWhiteSpace(referenceBlock) ? string.Empty : referenceBlock + "\n\n")}}
                                If uncertain, leave fields null/[] instead of guessing.
                                
                                CV text:
                                <<<
                                {{normalizedText}}
                                >>>
                                """
                }
            },
            temperature = 0.1,
            max_tokens = options.MaxCompletionTokens,
            response_format = new { type = "json_object" }
        };

        var requestUri =
            $"{options.Endpoint.TrimEnd('/')}/openai/deployments/{options.DeploymentName}/chat/completions?api-version={options.ApiVersion}";

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            await _diagnosticsLogger.LogAsync(
                "parser.request",
                $"attempt={attempt} preparedChars={normalizedText.Length} maxTokens={options.MaxCompletionTokens}",
                cancellationToken);

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("api-key", options.ApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                await _diagnosticsLogger.LogAsync(
                    "parser.response",
                    $"attempt={attempt} statusCode={(int)response.StatusCode} success=true",
                    cancellationToken);
                try
                {
                    return await ParseModelResponseAsync(responseContent, cancellationToken);
                }
                catch (JsonException ex)
                {
                    var finishReason = TryExtractFinishReason(responseContent);
                    await _diagnosticsLogger.LogAsync(
                        "parser.retry-json",
                        $"attempt={attempt} finishReason={finishReason ?? "unknown"} preview={BuildPreview(responseContent)}",
                        cancellationToken);
                    _logger.LogWarning(
                        ex,
                        "CV parsing returned malformed JSON (attempt {Attempt}/{MaxAttempts}). FinishReason={FinishReason}.",
                        attempt,
                        MaxAttempts,
                        finishReason ?? "unknown");

                    if (attempt < MaxFormatRetryAttempts)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
                        continue;
                    }

                    throw new AiServiceException(
                        "AI parser returned incomplete JSON for this CV. Please retry or reduce input size.",
                        502);
                }
                catch (InvalidOperationException ex)
                {
                    await _diagnosticsLogger.LogAsync(
                        "parser.retry-invalid",
                        $"attempt={attempt} preview={BuildPreview(responseContent)}",
                        cancellationToken);
                    _logger.LogWarning(
                        ex,
                        "CV parsing returned an unusable response (attempt {Attempt}/{MaxAttempts}).",
                        attempt,
                        MaxAttempts);

                    if (attempt < MaxFormatRetryAttempts)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
                        continue;
                    }

                    throw new AiServiceException(
                        "AI parser returned an invalid response format. Please retry.",
                        502);
                }
            }

            var statusCode = (int)response.StatusCode;
            var retryAfterSeconds = ExtractRetryAfterSeconds(response);
            var serviceMessage = ExtractServiceErrorMessage(responseContent);
            var finalMessage = BuildErrorMessage(statusCode, serviceMessage);
            await _diagnosticsLogger.LogAsync(
                "parser.response",
                $"attempt={attempt} statusCode={statusCode} retryAfterSeconds={(retryAfterSeconds?.ToString() ?? "n/a")} message={finalMessage}",
                cancellationToken);

            var isRetryable = response.StatusCode == HttpStatusCode.TooManyRequests ||
                              (int)response.StatusCode >= 500;

            if (isRetryable && attempt < MaxAttempts)
            {
                var waitSeconds = retryAfterSeconds ?? attempt * 2;
                _logger.LogWarning(
                    "CV parsing request failed (attempt {Attempt}/{MaxAttempts}). StatusCode={StatusCode}. Retrying in {WaitSeconds}s.",
                    attempt,
                    MaxAttempts,
                    statusCode,
                    waitSeconds);
                await Task.Delay(TimeSpan.FromSeconds(waitSeconds), cancellationToken);
                continue;
            }

            _logger.LogWarning(
                "CV parsing request failed. StatusCode={StatusCode}. Message={Message}",
                statusCode,
                finalMessage);
            throw new AiServiceException(finalMessage, statusCode, retryAfterSeconds);
        }

        throw new AiServiceException("AI parsing failed after retries.", 502);
    }

    private static void ValidateOptions(CvParsingOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint) ||
            string.IsNullOrWhiteSpace(options.ApiKey) ||
            string.IsNullOrWhiteSpace(options.DeploymentName))
        {
            throw new InvalidOperationException(
                "CV parsing is not configured. Set CvParsing:Endpoint, CvParsing:ApiKey, and CvParsing:DeploymentName.");
        }
    }

    private static string PrepareCvText(string rawText, int maxTextChars)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return string.Empty;
        }

        var normalized = CompactRawText(rawText);
        var paragraphs = normalized
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CleanParagraph)
            .Where(p => p.Length > 0)
            .ToArray();

        if (paragraphs.Length == 0)
        {
            return TrimToMax(CleanParagraph(normalized), maxTextChars);
        }

        var selectedParagraphs = SelectRelevantParagraphs(paragraphs);
        var merged = string.Join("\n\n", selectedParagraphs);
        return TrimToMax(merged, maxTextChars);
    }

    private static IReadOnlyList<string> SelectRelevantParagraphs(IReadOnlyList<string> paragraphs)
    {
        var selected = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var activeSectionWindow = 0;

        foreach (var paragraph in paragraphs.Take(3))
        {
            AddParagraph(selected, seen, paragraph);
        }

        foreach (var paragraph in paragraphs.Skip(3))
        {
            var lower = paragraph.ToLowerInvariant();
            var startsRelevantSection = SectionHeadings.Any(marker => lower.Contains(marker, StringComparison.Ordinal));

            if (startsRelevantSection)
            {
                activeSectionWindow = DefaultSectionWindowSize;
                AddParagraph(selected, seen, paragraph);
                continue;
            }

            if (activeSectionWindow > 0 || ShouldKeepParagraph(paragraph) || LooksLikeExperienceParagraph(paragraph))
            {
                AddParagraph(selected, seen, paragraph);

                if (activeSectionWindow > 0)
                {
                    activeSectionWindow--;
                }
            }
        }

        if (selected.Count < Math.Min(12, paragraphs.Count))
        {
            foreach (var paragraph in paragraphs.Take(30))
            {
                AddParagraph(selected, seen, paragraph);
            }
        }

        return selected;
    }

    private async Task<ParsedCandidateProfile> ParseModelResponseAsync(string responseContent, CancellationToken cancellationToken)
    {
        using var completion = JsonDocument.Parse(responseContent);
        var choices = completion.RootElement.GetProperty("choices");
        if (choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Model returned no choices.");
        }

        var message = choices[0].GetProperty("message");
        var contentElement = message.GetProperty("content");
        var contentText = ExtractMessageContent(contentElement);
        var cleanedJson = StripMarkdownCodeFence(contentText);

        var payload = JsonSerializer.Deserialize<ModelOutput>(cleanedJson, JsonOptions);
        if (payload is null)
        {
            throw new InvalidOperationException("Model output was empty.");
        }

        var experiences = await NormalizeExperiencesAsync(payload.Experiences, cancellationToken);
        var roleExperience = RoleExperienceCalculator.Calculate(experiences);
        var totalExperienceYears = experiences.Count > 0
            ? RoleExperienceCalculator.CalculateTotalYears(experiences)
            : (int)Math.Round(Math.Max(payload.ExperienceYears ?? 0d, 0d), MidpointRounding.AwayFromZero);

        return new ParsedCandidateProfile(
            payload.Name?.Trim() ?? string.Empty,
            payload.Email?.Trim() ?? string.Empty,
            payload.PhoneNumber?.Trim() ?? string.Empty,
            payload.HighestEducation?.Trim() ?? string.Empty,
            await NormalizeRoleListAsync(null, experiences, cancellationToken),
            NormalizeCompanies(experiences),
            NormalizeList(payload.Skills),
            NormalizeList(payload.Certifications),
            totalExperienceYears,
            experiences,
            roleExperience);
    }

    private static IReadOnlyList<string> NormalizeList(IEnumerable<string>? items)
    {
        return items?
                   .Where(static item => !string.IsNullOrWhiteSpace(item))
                   .Select(static item => item.Trim())
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .ToArray()
               ?? [];
    }

    private async Task<IReadOnlyList<ParsedExperienceEntry>> NormalizeExperiencesAsync(
        IEnumerable<ModelExperienceEntry>? items,
        CancellationToken cancellationToken)
    {
        if (items is null)
        {
            return [];
        }

        var result = new List<ParsedExperienceEntry>();
        foreach (var item in items)
        {
            var match = await _roleStandardizationService.MatchRoleAsync(
                (item.Role ?? string.Empty).Trim(),
                (item.Description ?? string.Empty).Trim(),
                cancellationToken);
            await _diagnosticsLogger.LogAsync(
                "role.match",
                $"source=parse rawRole={BuildPreview(item.Role ?? string.Empty)} standardRole={match.StandardRoleName} strategy={match.MatchStrategy} confidence={match.MatchConfidence:0.00} needsReview={match.NeedsReview} details={match.MatchDetails}",
                cancellationToken);

            var normalizedItem = new ParsedExperienceEntry(
                (item.CompanyName ?? string.Empty).Trim(),
                match.RawRoleTitle,
                match.StandardRoleId,
                match.StandardRoleName,
                match.MatchConfidence,
                match.NeedsReview,
                false,
                (item.StartDate ?? string.Empty).Trim(),
                (item.EndDate ?? string.Empty).Trim(),
                (item.Description ?? string.Empty).Trim());

            if (string.IsNullOrWhiteSpace(normalizedItem.CompanyName) &&
                string.IsNullOrWhiteSpace(normalizedItem.RawRoleTitle) &&
                string.IsNullOrWhiteSpace(normalizedItem.StandardRoleName) &&
                string.IsNullOrWhiteSpace(normalizedItem.Description))
            {
                continue;
            }

                result.Add(normalizedItem);
            if (result.Count == 12)
            {
                break;
            }
        }

        return result;
    }

    private async Task<IReadOnlyList<string>> NormalizeRoleListAsync(
        IEnumerable<string>? rolesFromModel,
        IReadOnlyList<ParsedExperienceEntry> normalizedExperiences,
        CancellationToken cancellationToken)
    {
        var direct = await _roleStandardizationService.StandardizeRoleListAsync(rolesFromModel, cancellationToken);
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

    private static string ExtractMessageContent(JsonElement contentElement)
    {
        if (contentElement.ValueKind == JsonValueKind.String)
        {
            return contentElement.GetString() ?? string.Empty;
        }

        if (contentElement.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var item in contentElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    sb.Append(item.GetString());
                    continue;
                }

                if (item.ValueKind == JsonValueKind.Object &&
                    item.TryGetProperty("text", out var textElement) &&
                    textElement.ValueKind == JsonValueKind.String)
                {
                    sb.Append(textElement.GetString());
                }
            }

            return sb.ToString();
        }

        return string.Empty;
    }

    private static string StripMarkdownCodeFence(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var lines = trimmed.Split('\n');
        if (lines.Length <= 2)
        {
            return trimmed;
        }

        return string.Join('\n', lines.Skip(1).SkipLast(1)).Trim();
    }

    private static int? ExtractRetryAfterSeconds(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is not null)
        {
            return Math.Max(1, (int)Math.Ceiling(response.Headers.RetryAfter.Delta.Value.TotalSeconds));
        }

        if (response.Headers.TryGetValues("x-ratelimit-reset-requests", out var values))
        {
            var raw = values.FirstOrDefault();
            if (int.TryParse(raw, out var parsed))
            {
                return Math.Max(1, parsed);
            }
        }

        return null;
    }

    private static string ExtractServiceErrorMessage(string responseContent)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            return string.Empty;
        }

        try
        {
            using var json = JsonDocument.Parse(responseContent);

            if (json.RootElement.TryGetProperty("error", out var errorNode))
            {
                if (errorNode.ValueKind == JsonValueKind.String)
                {
                    return errorNode.GetString() ?? string.Empty;
                }

                if (errorNode.ValueKind == JsonValueKind.Object &&
                    errorNode.TryGetProperty("message", out var messageNode) &&
                    messageNode.ValueKind == JsonValueKind.String)
                {
                    return messageNode.GetString() ?? string.Empty;
                }
            }
        }
        catch
        {
            // ignore parse errors and fall through
        }

        return string.Empty;
    }

    private static string BuildErrorMessage(int statusCode, string serviceMessage)
    {
        if (statusCode == 429)
        {
            var suffix = string.IsNullOrWhiteSpace(serviceMessage) ? string.Empty : $" Details: {serviceMessage}";
            return $"Azure OpenAI rate limit reached. Please wait and retry.{suffix}";
        }

        if (!string.IsNullOrWhiteSpace(serviceMessage))
        {
            return $"Azure OpenAI request failed ({statusCode}): {serviceMessage}";
        }

        return $"Azure OpenAI request failed ({statusCode}).";
    }

    private static string BuildPreview(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var compact = Regex.Replace(text, @"\s+", " ").Trim();
        return compact.Length <= 240 ? compact : compact[..240];
    }

    private static string? TryExtractFinishReason(string responseContent)
    {
        try
        {
            using var json = JsonDocument.Parse(responseContent);
            if (!json.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            {
                return null;
            }

            var firstChoice = choices[0];
            if (!firstChoice.TryGetProperty("finish_reason", out var finishReasonElement))
            {
                return null;
            }

            return finishReasonElement.GetString();
        }
        catch
        {
            return null;
        }
    }

    private static bool ShouldKeepParagraph(string paragraph)
    {
        var lower = paragraph.ToLowerInvariant();

        if (Regex.IsMatch(paragraph, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase))
        {
            return true;
        }

        if (Regex.IsMatch(paragraph, @"\+?\d[\d\s().-]{6,}"))
        {
            return true;
        }

        if (lower.Contains("http://") || lower.Contains("https://") || lower.Contains("linkedin.com"))
        {
            return true;
        }

        if (RelevantParagraphMarkers.Any(marker => lower.Contains(marker)))
        {
            return true;
        }

        // Drop obvious noise that frequently appears in CV footers.
        if (lower.Contains("references available") || lower.Contains("curriculum vitae"))
        {
            return false;
        }

        return false;
    }

    private static string CompactRawText(string rawText)
    {
        var normalized = rawText.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');
        var seenShortLines = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var compacted = new List<string>(lines.Length);
        var previousWasBlank = false;

        foreach (var rawLine in lines)
        {
            var line = CleanLine(rawLine);
            if (line.Length == 0)
            {
                if (!previousWasBlank)
                {
                    compacted.Add(string.Empty);
                    previousWasBlank = true;
                }

                continue;
            }

            if (ShouldDropLine(line))
            {
                continue;
            }

            if (line.Length <= 120)
            {
                seenShortLines.TryGetValue(line, out var count);
                if (count >= MaxRepeatedShortLineOccurrences)
                {
                    continue;
                }

                seenShortLines[line] = count + 1;
            }

            compacted.Add(line);
            previousWasBlank = false;
        }

        return string.Join('\n', compacted).Trim();
    }

    private static string CleanLine(string line)
    {
        return Regex.Replace(line, @"\s+", " ").Trim();
    }

    private static bool ShouldDropLine(string line)
    {
        var lower = line.ToLowerInvariant();

        if (lower is "curriculum vitae" or "resume" or "cv")
        {
            return true;
        }

        if (lower.Contains("references available"))
        {
            return true;
        }

        if (Regex.IsMatch(line, @"^page\s+\d+(\s+of\s+\d+)?$", RegexOptions.IgnoreCase))
        {
            return true;
        }

        if (Regex.IsMatch(line, @"^\d+\s*/\s*\d+$"))
        {
            return true;
        }

        return false;
    }

    private static bool LooksLikeExperienceParagraph(string paragraph)
    {
        var lower = paragraph.ToLowerInvariant();

        if (Regex.IsMatch(paragraph, @"\b(19|20)\d{2}\b"))
        {
            return true;
        }

        if (Regex.IsMatch(paragraph, @"\b(jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec)[a-z]*\b", RegexOptions.IgnoreCase))
        {
            return true;
        }

        if (lower.Contains("present") || lower.Contains("current"))
        {
            return true;
        }

        return false;
    }

    private static void AddParagraph(ICollection<string> selected, ISet<string> seen, string paragraph)
    {
        if (seen.Add(paragraph))
        {
            selected.Add(paragraph);
        }
    }

    private static string CleanParagraph(string paragraph)
    {
        var cleaned = Regex.Replace(paragraph, @"[ \t]+", " ");
        cleaned = Regex.Replace(cleaned, @"\n{3,}", "\n\n");
        return cleaned.Trim();
    }

    private static string TrimToMax(string text, int maxTextChars)
    {
        if (maxTextChars <= 0 || text.Length <= maxTextChars)
        {
            return text;
        }

        return text[..maxTextChars];
    }

    private static ParsedCandidateProfile BuildMockProfile(string cvText)
    {
        var lines = cvText.Split('\n')
            .Select(static l => l.Trim())
            .Where(static l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        var firstLine = lines.FirstOrDefault() ?? string.Empty;
        var candidateName = firstLine.Contains('@') ? string.Empty : firstLine;

        var email = Regex.Match(cvText, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase)
            .Value;
        var phone = Regex.Match(cvText, @"\+?\d[\d\s().-]{6,}").Value;

        var experienceYears = 0;
        var yearsMatch = Regex.Match(cvText, @"(\d{1,2})\+?\s+years?", RegexOptions.IgnoreCase);
        if (yearsMatch.Success && int.TryParse(yearsMatch.Groups[1].Value, out var parsedYears))
        {
            experienceYears = parsedYears;
        }

        var skills = KnownSkills
            .Where(skill => cvText.Contains(skill, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return new ParsedCandidateProfile(
            candidateName,
            email,
            phone,
            string.Empty,
            [],
            [],
            skills,
            [],
            experienceYears,
            [],
            []);
    }

    private sealed record ModelOutput(
        string? Name,
        string? Email,
        string? PhoneNumber,
        string? HighestEducation,
        IReadOnlyList<string>? Skills,
        IReadOnlyList<string>? Certifications,
        double? ExperienceYears,
        IReadOnlyList<ModelExperienceEntry>? Experiences);

    private static IReadOnlyList<string> NormalizeCompanies(IReadOnlyList<ParsedExperienceEntry> experiences)
    {
        return experiences
            .Select(static experience => experience.CompanyName)
            .Where(static company => !string.IsNullOrWhiteSpace(company))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private sealed record ModelExperienceEntry(
        string? CompanyName,
        string? Role,
        string? StartDate,
        string? EndDate,
        string? Description);
}
