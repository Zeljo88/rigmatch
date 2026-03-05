using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using RigMatch.Api.Models;

namespace RigMatch.Api.Services;

public sealed class CvParsingService : ICvParsingService
{
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
    private readonly IOptions<CvParsingOptions> _options;
    private readonly ILogger<CvParsingService> _logger;

    public CvParsingService(
        HttpClient httpClient,
        IWebHostEnvironment environment,
        IOptions<CvParsingOptions> options,
        ILogger<CvParsingService> logger)
    {
        _httpClient = httpClient;
        _environment = environment;
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
        var normalizedText = NormalizeCvTextLength(cvText, options.MaxTextChars);

        if (_environment.IsDevelopment() && options.UseMockInDevelopment)
        {
            _logger.LogInformation("Using mock CV parsing in Development environment.");
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
                    content =
                        "You extract structured candidate data from CV text. Return ONLY valid JSON with this exact shape: {\"name\":\"\",\"email\":\"\",\"jobTitles\":[],\"companies\":[],\"skills\":[],\"certifications\":[],\"experienceYears\":0}. Use empty strings, empty arrays, and 0 for missing values."
                },
                new
                {
                    role = "user",
                    content = $"Extract candidate details from the CV below and return JSON only:\n\n{normalizedText}"
                }
            },
            temperature = 0.1,
            max_tokens = 900,
            response_format = new { type = "json_object" }
        };

        var requestUri =
            $"{options.Endpoint.TrimEnd('/')}/openai/deployments/{options.DeploymentName}/chat/completions?api-version={options.ApiVersion}";

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("api-key", options.ApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("CV parsing request failed. StatusCode={StatusCode}", (int)response.StatusCode);
            throw new HttpRequestException("Azure OpenAI request failed.");
        }

        return ParseModelResponse(responseContent);
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

    private static string NormalizeCvTextLength(string text, int maxTextChars)
    {
        if (maxTextChars <= 0 || text.Length <= maxTextChars)
        {
            return text;
        }

        return text[..maxTextChars];
    }

    private static ParsedCandidateProfile ParseModelResponse(string responseContent)
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

        return new ParsedCandidateProfile(
            payload.Name?.Trim() ?? string.Empty,
            payload.Email?.Trim() ?? string.Empty,
            NormalizeList(payload.JobTitles),
            NormalizeList(payload.Companies),
            NormalizeList(payload.Skills),
            NormalizeList(payload.Certifications),
            Math.Max(payload.ExperienceYears, 0));
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
            [],
            [],
            skills,
            [],
            experienceYears);
    }

    private sealed record ModelOutput(
        string? Name,
        string? Email,
        IReadOnlyList<string>? JobTitles,
        IReadOnlyList<string>? Companies,
        IReadOnlyList<string>? Skills,
        IReadOnlyList<string>? Certifications,
        int ExperienceYears);
}
