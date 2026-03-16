using System.Globalization;
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
    private const int DefaultSectionWindowSize = 14;
    private const int MaxRepeatedShortLineOccurrences = 2;
    private const int MaxFormatRetryAttempts = 2;
    private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;
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
    private static readonly string[] CertificationSignalTokens =
    [
        "bosiet",
        "foet",
        "mist",
        "iwcf",
        "nebosh",
        "iosh",
        "h2s",
        "opito",
        "compex",
        "loto",
        "api ",
        "api-",
        "ndt",
        "nace",
        "gwo",
        "first aid",
        "manual handling",
        "confined space",
        "forklift",
        "offshore medical",
        "imca",
        "primevera p6",
        "primavera p6",
        "sap pm",
        "sap mm",
        "prince2",
        "pmp",
        "lean six sigma"
    ];
    private static readonly string[] TitleStopPhrases =
    [
        " with ",
        " supporting ",
        " support ",
        " responsible for ",
        " working on ",
        " experienced in ",
        " covering ",
        " across ",
        " during ",
        " for ",
        " and ",
        " including "
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
        "professional experience",
        "career history",
        "expérience",
        "experiencia",
        "education",
        "formation",
        "educación",
        "skills",
        "competencies",
        "compétences",
        "habilidades",
        "certification",
        "certifications",
        "certificate",
        "certificat",
        "certificación",
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
        "expérience",
        "expériences",
        "experiencia",
        "experiencia laboral",
        "education",
        "formation",
        "educación",
        "skills",
        "competencies",
        "compétences",
        "habilidades",
        "certification",
        "certifications",
        "certificate",
        "certificat",
        "certificación"
    ];
    private static readonly string[] RoleSignalTokens =
    [
        "engineer", "eng", "manager", "lead", "specialist", "supervisor", "advisor", "consultant", "analyst",
        "coordinator", "operator", "technician", "tech", "inspector", "planner", "architect", "director", "head",
        "petroleum", "reservoir", "production", "drilling", "completion", "intervention", "process", "facilities",
        "operations", "hse", "hsse", "ehs", "geologist", "geophysicist"
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
        var experienceHintBlock = BuildExperienceHintBlock(
            cvText,
            options.MaxExperienceHintChars,
            options.MaxExperienceHintSnippetChars);
        var promptChars = normalizedText.Length + referenceBlock.Length + experienceHintBlock.Length;
        await _diagnosticsLogger.LogAsync(
            "parser.prepare",
            $"rawChars={cvText.Length} preparedChars={normalizedText.Length} referenceChars={referenceBlock.Length} experienceHintChars={experienceHintBlock.Length} promptChars={promptChars} maxTextChars={options.MaxTextChars} maxCompletionTokens={options.MaxCompletionTokens} maxExperienceHintChars={options.MaxExperienceHintChars}",
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
                              - Preserve chronology: one experience object per role/company/date block.
                              - Preserve the raw job title exactly when present, even if uncommon, internal, all-caps, multilingual, or noisy.
                              - Do not replace specific titles with generic titles like "Engineer" if the CV provides a more precise role.
                              - Use chronology hints when they help separate company boundaries, but do not invent missing jobs.
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
                                {{(string.IsNullOrWhiteSpace(experienceHintBlock) ? string.Empty : experienceHintBlock + "\n\n")}}
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
                    return await ParseModelResponseAsync(responseContent, cvText, cancellationToken);
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

        if (options.MaxTextChars <= 0 || options.MaxCompletionTokens <= 0)
        {
            throw new InvalidOperationException(
                "CV parsing limits are invalid. Set CvParsing:MaxTextChars and CvParsing:MaxCompletionTokens to positive numbers.");
        }

        if (options.MaxExperienceHintChars < 0 || options.MaxExperienceHintSnippetChars < 0)
        {
            throw new InvalidOperationException(
                "CV parsing hint limits are invalid. Set CvParsing:MaxExperienceHintChars and CvParsing:MaxExperienceHintSnippetChars to zero or positive numbers.");
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

    private async Task<ParsedCandidateProfile> ParseModelResponseAsync(string responseContent, string rawCvText, CancellationToken cancellationToken)
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
        var jobTitles = experiences
            .Select(exp => exp.RawRoleTitle)
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var normalizedSkills = NormalizeSkillList(payload.Skills);
        var certificationHints = ExtractCertificationHints(rawCvText);
        var normalizedCertifications = NormalizeCertificationList(payload.Certifications, normalizedSkills, certificationHints);
        normalizedSkills = RemoveCertificationLikeSkills(normalizedSkills, normalizedCertifications);
        var normalizedName = NormalizeCandidateName(payload.Name, rawCvText);

        return new ParsedCandidateProfile(
            normalizedName,
            payload.Email?.Trim() ?? string.Empty,
            payload.PhoneNumber?.Trim() ?? string.Empty,
            payload.HighestEducation?.Trim() ?? string.Empty,
            jobTitles,
            NormalizeCompanies(experiences),
            normalizedSkills,
            normalizedCertifications,
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

    private static IReadOnlyList<string> NormalizeSkillList(IEnumerable<string>? items)
    {
        return NormalizeList(items)
            .Where(item => !LooksLikeCertification(item))
            .ToArray();
    }

    private static IReadOnlyList<string> NormalizeCertificationList(IEnumerable<string>? certifications, IEnumerable<string>? skills, IEnumerable<string>? hints = null)
    {
        var candidates = NormalizeList(certifications)
            .Concat(NormalizeList(skills).Where(LooksLikeCertification))
            .Concat(NormalizeList(hints))
            .ToList();

        if (candidates.Any(item => item.Equals("Rigging", StringComparison.OrdinalIgnoreCase)) &&
            candidates.Any(item => item.Equals("Slinging", StringComparison.OrdinalIgnoreCase)))
        {
            candidates.RemoveAll(item => item.Equals("Rigging", StringComparison.OrdinalIgnoreCase) || item.Equals("Slinging", StringComparison.OrdinalIgnoreCase));
            candidates.Add("Rigging & Slinging");
        }

        if (candidates.Any(item => item.Contains("OPITO", StringComparison.OrdinalIgnoreCase)) &&
            candidates.Any(item => item.Contains("FOET", StringComparison.OrdinalIgnoreCase)) &&
            !candidates.Any(item => item.Equals("OPITO FOET", StringComparison.OrdinalIgnoreCase)))
        {
            candidates.RemoveAll(item => item.Equals("OPITO", StringComparison.OrdinalIgnoreCase) || item.Equals("FOET", StringComparison.OrdinalIgnoreCase));
            candidates.Add("OPITO FOET");
        }

        return candidates
            .Select(CanonicalizeCertification)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> RemoveCertificationLikeSkills(IEnumerable<string> skills, IEnumerable<string> certifications)
    {
        var certificationSet = new HashSet<string>(certifications.Select(NormalizeForComparison), StringComparer.OrdinalIgnoreCase);
        return skills
            .Where(skill => !LooksLikeCertification(skill) && !certificationSet.Contains(NormalizeForComparison(skill)))
            .ToArray();
    }

    private static bool LooksLikeCertification(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = NormalizeForComparison(value);
        return CertificationSignalTokens.Any(token => normalized.Contains(NormalizeForComparison(token), StringComparison.Ordinal));
    }

    private static string CanonicalizeCertification(string value)
    {
        var cleaned = CleanExperienceField(value, 80);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return string.Empty;
        }

        var normalized = NormalizeForComparison(cleaned);
        return normalized switch
        {
            "bosiet" => "BOSIET",
            "mist" => "MIST",
            "h2s" => "H2S",
            "iwcf level 4" or "siwcf level 4" => "IWCF Level 4",
            "prince2 foundation" => "Prince2 Foundation",
            "compex awareness" => "CompEx Awareness",
            "opito foet" or "foet" => "OPITO FOET",
            "loto" => "LOTO",
            _ => NormalizeShoutyText(cleaned)
        };
    }

    private static string NormalizeForComparison(string value)
    {
        return Regex.Replace(value ?? string.Empty, @"[^a-z0-9]+", " ", RegexOptions.IgnoreCase)
            .Trim()
            .ToLowerInvariant();
    }

    private static IReadOnlyList<string> ExtractCertificationHints(string rawCvText)
    {
        if (string.IsNullOrWhiteSpace(rawCvText))
        {
            return [];
        }

        var hints = new List<string>();
        var lines = rawCvText.Replace("\r\n", "\n")
            .Split('\n')
            .Select(CleanExperienceField)
            .Where(line => line.Length > 0)
            .ToArray();

        for (var i = 0; i < lines.Length; i++)
        {
            var lower = lines[i].ToLowerInvariant();
            if (!(lower.StartsWith("certifications") || lower.StartsWith("certification") || lower.StartsWith("licenses") || lower.StartsWith("tickets")))
            {
                continue;
            }

            var inline = lines[i].Split(':', 2);
            if (inline.Length == 2)
            {
                hints.AddRange(SplitDelimitedValues(inline[1]));
            }

            for (var j = i + 1; j < Math.Min(i + 4, lines.Length); j++)
            {
                if (IsHardSectionBoundary(lines[j]))
                {
                    break;
                }

                hints.AddRange(SplitDelimitedValues(lines[j]));
            }
        }

        return hints
            .Select(CleanExperienceField)
            .Where(LooksLikeCertification)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> SplitDelimitedValues(string value)
    {
        return (value ?? string.Empty)
            .Split([',', ';', '|', '•'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CleanExperienceField)
            .Where(item => item.Length > 0);
    }

    private static string NormalizeCandidateName(string? modelName, string rawCvText)
    {
        var cleanedModelName = CleanExperienceField(modelName, 120);
        var hint = ExtractNameHint(rawCvText);
        if (string.IsNullOrWhiteSpace(cleanedModelName))
        {
            return hint;
        }

        if (!string.IsNullOrWhiteSpace(hint) &&
            (cleanedModelName.Contains(hint, StringComparison.OrdinalIgnoreCase) || hint.Contains(cleanedModelName, StringComparison.OrdinalIgnoreCase)))
        {
            return hint.Length <= cleanedModelName.Length ? hint : cleanedModelName;
        }

        return NormalizeShoutyText(cleanedModelName);
    }

    private static string ExtractNameHint(string rawCvText)
    {
        if (string.IsNullOrWhiteSpace(rawCvText))
        {
            return string.Empty;
        }

        var lines = rawCvText.Replace("\r\n", "\n")
            .Split('\n')
            .Select(CleanExperienceField)
            .Where(line => line.Length > 0)
            .Take(8)
            .ToArray();

        foreach (var line in lines)
        {
            if (line.Contains('@') || Regex.IsMatch(line, @"\d") || LooksLikeRoleText(line) || IsHardSectionBoundary(line))
            {
                continue;
            }

            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length is >= 2 and <= 4 && words.All(word => word.All(ch => char.IsLetter(ch) || ch is '-' or '\'')))
            {
                return NormalizeShoutyText(line);
            }
        }

        return string.Empty;
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
            var cleaned = NormalizeModelExperienceEntry(item);
            var roleForMatching = !string.IsNullOrWhiteSpace(cleaned.Role)
                ? cleaned.Role
                : InferRoleFromDescription(cleaned.Description);

            var match = await _roleStandardizationService.MatchRoleAsync(
                roleForMatching,
                cleaned.Description,
                cancellationToken);
            await _diagnosticsLogger.LogAsync(
                "role.match",
                $"source=parse rawRole={BuildPreview(roleForMatching ?? string.Empty)} standardRole={match.StandardRoleName} strategy={match.MatchStrategy} confidence={match.MatchConfidence:0.00} needsReview={match.NeedsReview} details={match.MatchDetails}",
                cancellationToken);

            var rawRoleTitle = ResolvePreferredRawRoleTitle(cleaned.Role, cleaned.Description, match.StandardRoleName);
            var normalizedItem = new ParsedExperienceEntry(
                cleaned.CompanyName,
                rawRoleTitle,
                match.StandardRoleId,
                match.StandardRoleName,
                match.MatchConfidence,
                match.NeedsReview || string.IsNullOrWhiteSpace(cleaned.Role),
                false,
                cleaned.StartDate,
                cleaned.EndDate,
                cleaned.Description);

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
        var directRaw = NormalizeList(rolesFromModel);
        if (directRaw.Count > 0)
        {
            return directRaw;
        }

        var standardizedInput = await _roleStandardizationService.StandardizeRoleListAsync(rolesFromModel, cancellationToken);
        if (standardizedInput.Count > 0)
        {
            return standardizedInput;
        }

        var rawTitles = normalizedExperiences
            .Select(exp => exp.RawRoleTitle)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (rawTitles.Length > 0)
        {
            return rawTitles;
        }

        return normalizedExperiences
            .Select(exp => exp.StandardRoleName)
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

    private static string BuildExperienceHintBlock(string rawText, int maxHintChars, int maxSnippetChars)
    {
        if (string.IsNullOrWhiteSpace(rawText) || maxHintChars <= 0 || maxSnippetChars <= 0)
        {
            return string.Empty;
        }

        var lines = rawText.Replace("\r\n", "\n")
            .Split('\n')
            .Select(CleanLine)
            .Where(line => line.Length > 0)
            .ToArray();

        if (lines.Length == 0)
        {
            return string.Empty;
        }

        var snippets = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < lines.Length; i++)
        {
            if (!LooksLikeChronologySignal(lines[i]))
            {
                continue;
            }

            var window = lines.Skip(i).Take(3)
                .TakeWhile(line => !IsHardSectionBoundary(line))
                .Select(NormalizeShoutyText)
                .Where(line => line.Length > 0)
                .ToArray();

            if (window.Length == 0)
            {
                continue;
            }

            var snippet = string.Join(" | ", window);
            if (snippet.Length > maxSnippetChars)
            {
                snippet = snippet[..maxSnippetChars].Trim();
            }

            if (seen.Add(snippet))
            {
                snippets.Add(snippet);
            }

            if (snippets.Count == 10)
            {
                break;
            }
        }

        if (snippets.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Possible chronology hints from the CV text (use only when clearly supported):");
        var appended = 0;
        foreach (var snippet in snippets)
        {
            var line = $"- {snippet}{Environment.NewLine}";
            if (builder.Length + line.Length > maxHintChars)
            {
                break;
            }

            builder.Append(line);
            appended++;
        }

        if (appended == 0)
        {
            return string.Empty;
        }

        return builder.ToString().Trim();
    }

    private static bool LooksLikeChronologySignal(string line)
    {
        if (LooksLikeExperienceParagraph(line))
        {
            return true;
        }

        if (Regex.IsMatch(line, @"\b(19|20)\d{2}\s*[-–/]\s*(19|20)\d{2}|present|current", RegexOptions.IgnoreCase))
        {
            return true;
        }

        return RoleSignalTokens.Any(token => line.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsHardSectionBoundary(string line)
    {
        var lower = line.ToLowerInvariant();
        return SectionHeadings.Any(heading => lower.Equals(heading, StringComparison.Ordinal) || lower.StartsWith(heading + ":", StringComparison.Ordinal));
    }

    private static (string CompanyName, string Role, string StartDate, string EndDate, string Description) NormalizeModelExperienceEntry(ModelExperienceEntry item)
    {
        var company = CleanExperienceField(item.CompanyName);
        var role = CleanExperienceField(item.Role);
        var startDate = CleanDateField(item.StartDate);
        var endDate = CleanDateField(item.EndDate);
        var description = CleanExperienceField(item.Description, 280);

        if (string.IsNullOrWhiteSpace(company) && !string.IsNullOrWhiteSpace(role))
        {
            var split = TrySplitRoleAndCompany(role);
            if (split is not null)
            {
                role = split.Value.Role;
                company = split.Value.Company;
            }
        }

        if (string.IsNullOrWhiteSpace(role) && !string.IsNullOrWhiteSpace(company))
        {
            var split = TrySplitRoleAndCompany(company);
            if (split is not null)
            {
                role = split.Value.Role;
                company = split.Value.Company;
            }
        }

        role = StripDateNoise(role);
        company = StripDateNoise(company);

        var normalizedRole = NormalizeShoutyText(role);
        var normalizedCompany = NormalizeShoutyText(company);
        var normalizedDescription = NormalizeShoutyText(description);

        if (TryRescueRoleCompanyCollision(normalizedRole, normalizedCompany, normalizedDescription) is { } rescued)
        {
            normalizedRole = rescued.Role;
            normalizedCompany = rescued.Company;
        }

        return (
            normalizedCompany,
            normalizedRole,
            startDate,
            endDate,
            normalizedDescription);
    }

    private static string CleanExperienceField(string? value, int maxLength = 120)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleaned = value.Trim()
            .Replace("ﬀ", "ff", StringComparison.Ordinal)
            .Replace("ﬁ", "fi", StringComparison.Ordinal)
            .Replace("ﬂ", "fl", StringComparison.Ordinal)
            .Replace("ﬃ", "ffi", StringComparison.Ordinal)
            .Replace("ﬄ", "ffl", StringComparison.Ordinal);
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        cleaned = cleaned.Trim('•', '-', '–', '|', ',', ';');
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength].Trim();
    }

    private static string CleanDateField(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleaned = CleanExperienceField(value, 32);
        cleaned = Regex.Replace(cleaned, @"\s+to\s+", "-", RegexOptions.IgnoreCase);
        return cleaned;
    }

    private static string StripDateNoise(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var withoutDates = Regex.Replace(
            value,
            @"\b((19|20)\d{2}([-/](0?[1-9]|1[0-2]))?)\b\s*(?:(?:-|–|/|to)\s*)*\b((19|20)\d{2}([-/](0?[1-9]|1[0-2]))?|present|current)?\b",
            string.Empty,
            RegexOptions.IgnoreCase);

        return CleanExperienceField(withoutDates);
    }

    private static (string Role, string Company)? TrySplitRoleAndCompany(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var atMatch = Regex.Match(value, @"^(?<role>.+?)\s+(?:at|@)\s+(?<company>.+)$", RegexOptions.IgnoreCase);
        if (atMatch.Success)
        {
            return (CleanExperienceField(atMatch.Groups["role"].Value), CleanExperienceField(atMatch.Groups["company"].Value));
        }

        var mergedUpperMatch = Regex.Match(value, @"^(?<role>(?:E\s*&\s*I\s+)?(?:[A-Z][A-Z&/]+(?:\s+[A-Z][A-Z&/]+){0,2}))(?<company>[A-Z][a-z][A-Za-z&.]+)$");
        if (mergedUpperMatch.Success)
        {
            var mergedRole = NormalizeShoutyText(CleanExperienceField(mergedUpperMatch.Groups["role"].Value));
            var mergedCompany = NormalizeShoutyText(CleanExperienceField(mergedUpperMatch.Groups["company"].Value));
            if (LooksLikeRoleText(mergedRole) && !LooksLikeRoleText(mergedCompany))
            {
                return (mergedRole, mergedCompany);
            }
        }

        var separatorMatch = Regex.Match(value, @"^(?<left>.+?)\s*(?:\||-|–|,|/)\s*(?<right>.+)$");
        if (!separatorMatch.Success)
        {
            return null;
        }

        var left = CleanExperienceField(separatorMatch.Groups["left"].Value);
        var right = CleanExperienceField(separatorMatch.Groups["right"].Value);
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return null;
        }

        var leftLooksRole = LooksLikeRoleText(left);
        var rightLooksRole = LooksLikeRoleText(right);

        if (leftLooksRole && !rightLooksRole)
        {
            return (left, right);
        }

        if (!leftLooksRole && rightLooksRole)
        {
            return (right, left);
        }

        return null;
    }

    private static bool LooksLikeRoleText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var lower = value.ToLowerInvariant();
        return RoleSignalTokens.Any(token => lower.Contains(token, StringComparison.Ordinal));
    }

    private static string ResolvePreferredRawRoleTitle(string role, string description, string standardRoleName)
    {
        var cleanedRole = CleanExperienceField(role);
        var cleanedDescription = CleanExperienceField(description);
        var cleanedStandardRole = CleanExperienceField(standardRoleName);

        if (!string.IsNullOrWhiteSpace(cleanedRole))
        {
            if (!string.IsNullOrWhiteSpace(cleanedDescription) && LooksLikeStandaloneRoleTitle(cleanedDescription) &&
                IsUsefulTitleExpansion(cleanedRole, cleanedDescription))
            {
                return NormalizeShoutyText(TrimRoleTail(cleanedDescription));
            }

            return NormalizeShoutyText(TrimRoleTail(cleanedRole));
        }

        if (!string.IsNullOrWhiteSpace(cleanedDescription) && LooksLikeStandaloneRoleTitle(cleanedDescription))
        {
            return NormalizeShoutyText(TrimRoleTail(cleanedDescription));
        }

        return NormalizeShoutyText(cleanedStandardRole);
    }

    private static bool LooksLikeStandaloneRoleTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var cleaned = CleanExperienceField(value);
        if (cleaned.Length == 0 || cleaned.Length > 60)
        {
            return false;
        }

        if (Regex.IsMatch(cleaned, @"[.;:]") || cleaned.Contains(','))
        {
            return false;
        }

        var wordCount = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount > 5)
        {
            return false;
        }

        var lower = $" {cleaned.ToLowerInvariant()} ";
        if (TitleStopPhrases.Any(lower.Contains))
        {
            return false;
        }

        return LooksLikeRoleText(cleaned);
    }

    private static string TrimRoleTail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = CleanExperienceField(value);
        var lower = $" {trimmed.ToLowerInvariant()} ";
        foreach (var stopPhrase in TitleStopPhrases)
        {
            var index = lower.IndexOf(stopPhrase, StringComparison.Ordinal);
            if (index <= 0)
            {
                continue;
            }

            var cut = Math.Min(index, trimmed.Length);
            return CleanExperienceField(trimmed[..cut]);
        }

        return trimmed;
    }

    private static bool IsUsefulTitleExpansion(string role, string description)
    {
        var normalizedRole = NormalizeForComparison(role);
        var normalizedDescription = NormalizeForComparison(description);
        if (string.IsNullOrWhiteSpace(normalizedRole) || string.IsNullOrWhiteSpace(normalizedDescription))
        {
            return false;
        }

        if (!normalizedDescription.Contains(normalizedRole, StringComparison.Ordinal) || normalizedDescription == normalizedRole)
        {
            return false;
        }

        var roleWords = normalizedRole.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var descriptionWords = normalizedDescription.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (descriptionWords.Length > roleWords.Length + 2)
        {
            return false;
        }

        return true;
    }

    private static (string Role, string Company)? TryRescueRoleCompanyCollision(string role, string company, string description)
    {
        if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(company))
        {
            return null;
        }

        if (!LooksLikeRoleText(company))
        {
            return null;
        }

        var inferredFromDescription = InferRoleFromDescription(description);
        var mergedCompany = ExtractCompanyFromMergedRole(role);
        if (string.IsNullOrWhiteSpace(mergedCompany))
        {
            return null;
        }

        var rescuedRole = company;
        if (!LooksLikeStandaloneRoleTitle(company) &&
            !string.IsNullOrWhiteSpace(inferredFromDescription) &&
            !rescuedRole.Contains(inferredFromDescription, StringComparison.OrdinalIgnoreCase) &&
            !inferredFromDescription.Contains(rescuedRole, StringComparison.OrdinalIgnoreCase))
        {
            rescuedRole = inferredFromDescription;
        }

        return (NormalizeShoutyText(rescuedRole), NormalizeShoutyText(mergedCompany));
    }

    private static string ExtractCompanyFromMergedRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role) || role.Contains(' '))
        {
            return string.Empty;
        }

        var compact = Regex.Replace(role, @"[^A-Za-z0-9&]+", string.Empty);
        if (compact.Length < 8)
        {
            return string.Empty;
        }

        foreach (var prefix in new[] { "TECH", "ENG", "ENGINEER", "SUPERVISOR", "MANAGER", "OPERATOR", "HELPER", "LEAD" })
        {
            if (compact.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && compact.Length > prefix.Length + 3)
            {
                return compact[prefix.Length..];
            }

            if (compact.EndsWith(prefix, StringComparison.OrdinalIgnoreCase) && compact.Length > prefix.Length + 3)
            {
                return compact[..^prefix.Length];
            }
        }

        return string.Empty;
    }

    private static string InferRoleFromDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        var lower = description.ToLowerInvariant();
        if ((lower.Contains("electrical") && lower.Contains("technician")) || lower.Contains("e&i tech") || lower.Contains("e and i tech"))
        {
            return "Electrical Technician";
        }

        if (lower.Contains("electrical") && lower.Contains("helper"))
        {
            return "Electrical Helper";
        }

        if (lower.Contains("reservoir") || lower.Contains("history matching") || lower.Contains("material balance"))
        {
            return "Reservoir Engineer";
        }

        if (lower.Contains("drilling") || lower.Contains("well control") || lower.Contains("bop"))
        {
            return "Drilling Engineer";
        }

        if (lower.Contains("completion") || lower.Contains("packer") || lower.Contains("sand control"))
        {
            return "Completion Engineer";
        }

        if (lower.Contains("wireline") || lower.Contains("coiled tubing") || lower.Contains("workover") || lower.Contains("intervention"))
        {
            return "Well Intervention Engineer";
        }

        if (lower.Contains("process") || lower.Contains("hazop") || lower.Contains("p&id"))
        {
            return "Process Engineer";
        }

        if (lower.Contains("production") || lower.Contains("artificial lift") || lower.Contains("flow assurance"))
        {
            return "Production Engineer";
        }

        if (lower.Contains("facilities") || lower.Contains("topsides") || lower.Contains("brownfield"))
        {
            return "Facilities Engineer";
        }

        if (lower.Contains("hse") || lower.Contains("hsse") || lower.Contains("safety") || lower.Contains("incident investigation"))
        {
            return "HSE Engineer";
        }

        if (lower.Contains("startup") || lower.Contains("shutdown") || lower.Contains("commissioning") || lower.Contains("operations readiness"))
        {
            return "Operations Engineer";
        }

        return string.Empty;
    }

    private static string NormalizeShoutyText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var letters = value.Count(char.IsLetter);
        if (letters < 6)
        {
            return value.Trim();
        }

        var upperLetters = value.Count(char.IsUpper);
        if (upperLetters * 1.0 / Math.Max(letters, 1) < 0.8d)
        {
            return value.Trim();
        }

        var titled = InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
        titled = Regex.Replace(titled, @"\bHse\b", "HSE");
        titled = Regex.Replace(titled, @"\bHsse\b", "HSSE");
        titled = Regex.Replace(titled, @"\bEhs\b", "EHS");
        return titled.Trim();
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
