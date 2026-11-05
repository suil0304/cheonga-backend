using cheongdo_backend.Guidance;

namespace cheongdo_backend.Tests;

public sealed class GuidanceServiceTests
{
    [Fact]
    public async Task MockProviderReturnsClearlyMarkedMatchingMaterial()
    {
        var provider = new MockEducationMaterialProvider();

        var results = await provider.SearchAsync(
            "인천 디지털배움터",
            "인천",
            CancellationToken.None);

        var material = Assert.Single(results);
        Assert.Equal("mock-digital-learning-center", material.Id);
        Assert.False(material.IsOfficial);
        Assert.True(material.IsMock);
        Assert.Null(material.SourceUrl);
    }

    [Fact]
    public async Task GuidanceServiceDoesNotSendMockMaterialToCopilot()
    {
        var agent = new TrackingAgent();
        var service = new GuidanceService(new MockEducationMaterialProvider(), agent);

        var response = await service.AnswerAsync(
            new GuidanceRequest("인천 디지털배움터", "인천"),
            CancellationToken.None);

        Assert.Equal("mock", response.Status);
        Assert.True(response.IsMock);
        Assert.Null(response.Answer);
        Assert.Equal(0, agent.CallCount);
    }

    [Fact]
    public async Task GuidanceServiceDoesNotGenerateAnAnswerWithoutSources()
    {
        var agent = new TrackingAgent();
        var service = new GuidanceService(new MockEducationMaterialProvider(), agent);

        var response = await service.AnswerAsync(
            new GuidanceRequest("없는 주제", null),
            CancellationToken.None);

        Assert.Equal("no_sources", response.Status);
        Assert.Null(response.Answer);
        Assert.Empty(response.Sources);
        Assert.Equal(0, agent.CallCount);
    }

    [Fact]
    public async Task GuidanceServiceDoesNotGenerateFromUnverifiedSources()
    {
        var agent = new TrackingAgent();
        var provider = new StaticMaterialProvider(
        [
            new EducationMaterial(
                "unverified-welfare-source",
                "welfare-program",
                "복지 지원 사업",
                "출처 확인 전 자료",
                "청도군",
                "미확인 출처",
                new Uri("https://example.invalid/source"),
                IsOfficial: false,
                IsMock: false,
                Keywords: ["복지"])
        ]);
        var service = new GuidanceService(provider, agent);

        var response = await service.AnswerAsync(
            new GuidanceRequest("복지 지원", "청도군"),
            CancellationToken.None);

        Assert.Equal("unverified", response.Status);
        Assert.Null(response.Answer);
        Assert.False(response.IsMock);
        Assert.Equal(0, agent.CallCount);
    }

    private sealed class TrackingAgent : IGuidanceAgent
    {
        public int CallCount { get; private set; }

        public Task<string> GenerateAnswerAsync(
            string contextSummary,
            IReadOnlyList<EducationMaterial> sources,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult("generated");
        }
    }

    private sealed class StaticMaterialProvider(IReadOnlyList<EducationMaterial> materials)
        : IEducationMaterialProvider
    {
        public bool IsMock => false;

        public Task<IReadOnlyList<EducationMaterial>> SearchAsync(
            string contextSummary,
            string? region,
            CancellationToken cancellationToken) =>
            Task.FromResult(materials);
    }
}
