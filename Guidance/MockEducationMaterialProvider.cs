namespace cheongdo_backend.Guidance;

public sealed class MockEducationMaterialProvider : IEducationMaterialProvider
{
    public bool IsMock => true;

    private static readonly EducationMaterial[] Materials =
    [
        new(
            "mock-digital-learning-center",
            "digital-learning-center",
            "디지털배움터 교육장 정보 (Mock)",
            "지역별 디지털 교육 장소를 찾는 흐름을 검증하기 위한 개발용 예시입니다.",
            "인천",
            "개발용 Mock 데이터",
            null,
            false,
            true,
            ["인천", "배움터", "교육장", "장소", "위치", "디지털"]),
        new(
            "mock-smartphone-basics",
            "digital-literacy-course",
            "스마트폰 기초 교육 과정 (Mock)",
            "스마트폰 기초 활용 교육 자료를 검색하는 흐름을 검증하기 위한 개발용 예시입니다.",
            null,
            "개발용 Mock 데이터",
            null,
            false,
            true,
            ["스마트폰", "기초", "교육", "앱", "디지털"]),
        new(
            "mock-welfare-program",
            "welfare-program",
            "복지 지원 사업 검색 결과 (Mock)",
            "지역과 대상 조건으로 복지 지원 사업을 찾는 흐름을 검증하기 위한 개발용 예시입니다.",
            "청도군",
            "개발용 Mock 데이터",
            null,
            false,
            true,
            ["복지", "지원", "사업", "청도군", "신청"])
    ];

    public Task<IReadOnlyList<EducationMaterial>> SearchAsync(
        string contextSummary,
        string? region,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var terms = contextSummary
            .Split([' ', ',', '.', '，', '、'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var results = Materials
            .Where(material =>
                string.IsNullOrWhiteSpace(region)
                || material.Region is null
                || string.Equals(material.Region, region, StringComparison.OrdinalIgnoreCase))
            .Where(material =>
                terms.Length == 0
                || terms.Any(term =>
                    material.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || material.Summary.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || material.Keywords.Contains(term, StringComparer.OrdinalIgnoreCase)))
            .ToArray();

        return Task.FromResult<IReadOnlyList<EducationMaterial>>(results);
    }
}
