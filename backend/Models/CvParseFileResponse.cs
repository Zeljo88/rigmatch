namespace RigMatch.Api.Models;

public sealed record CvParseFileResponse(
    string OriginalFileName,
    long SizeBytes,
    string StoragePath,
    int ExtractedTextLength,
    ParsedCandidateProfile ParsedProfile,
    DateTimeOffset ParsedAtUtc);
