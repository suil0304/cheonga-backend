namespace cheongdo_backend.Guidance;

public sealed record EducationMaterial(
    string Id,
    string Category,
    string Title,
    string Summary,
    string? Region,
    string SourceName,
    Uri? SourceUrl,
    bool IsOfficial,
    bool IsMock,
    IReadOnlyList<string> Keywords);

public sealed record GuidanceRequest(string ContextSummary, string? Region);

public sealed record GuidanceResponse(
    string Status,
    string Message,
    string? Answer,
    IReadOnlyList<EducationMaterial> Sources,
    bool IsMock);
