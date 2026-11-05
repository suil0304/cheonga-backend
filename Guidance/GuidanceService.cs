namespace cheongdo_backend.Guidance;

public sealed class GuidanceService(
    IEducationMaterialProvider materialProvider,
    IGuidanceAgent agent)
{
    public async Task<GuidanceResponse> AnswerAsync(
        GuidanceRequest request,
        CancellationToken cancellationToken)
    {
        var sources = await materialProvider.SearchAsync(
            request.ContextSummary,
            request.Region,
            cancellationToken);

        if (sources.Count == 0)
        {
            return new(
                "no_sources",
                "요청에 맞는 자료를 찾지 못했습니다. 확인되지 않은 안내는 제공하지 않습니다.",
                null,
                sources,
                IsMock: materialProvider.IsMock);
        }

        if (sources.Any(source => source.IsMock || !source.IsOfficial || source.SourceUrl is null))
        {
            return new(
                sources.Any(source => source.IsMock) ? "mock" : "unverified",
                sources.Any(source => source.IsMock)
                    ? "개발용 Mock 자료입니다. 실제 공식 안내나 복지 자격 판단에 사용하지 마세요."
                    : "공식 출처를 검증할 수 없어 안내를 생성하지 않았습니다.",
                null,
                sources,
                IsMock: sources.Any(source => source.IsMock));
        }

        var answer = await agent.GenerateAnswerAsync(
            request.ContextSummary,
            sources,
            cancellationToken);

        return new(
            "grounded",
            "확인된 자료를 바탕으로 작성했습니다.",
            answer,
            sources,
            IsMock: false);
    }
}
