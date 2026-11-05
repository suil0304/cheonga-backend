namespace cheongdo_backend.Guidance;

public interface IEducationMaterialProvider
{
    bool IsMock { get; }

    Task<IReadOnlyList<EducationMaterial>> SearchAsync(
        string contextSummary,
        string? region,
        CancellationToken cancellationToken);
}
