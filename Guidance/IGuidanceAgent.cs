namespace cheongdo_backend.Guidance;

public interface IGuidanceAgent
{
    Task<string> GenerateAnswerAsync(
        string contextSummary,
        IReadOnlyList<EducationMaterial> sources,
        CancellationToken cancellationToken);
}
