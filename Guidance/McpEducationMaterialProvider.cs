using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace cheongdo_backend.Guidance;

public sealed class McpEducationMaterialProvider(Uri endpoint) : IEducationMaterialProvider
{
    public bool IsMock => true;

    public async Task<IReadOnlyList<EducationMaterial>> SearchAsync(
        string contextSummary,
        string? region,
        CancellationToken cancellationToken)
    {
        var transport = new HttpClientTransport(new()
        {
            Endpoint = endpoint,
            TransportMode = HttpTransportMode.StreamableHttp
        });
        await using var client = await McpClient.CreateAsync(
            transport,
            cancellationToken: cancellationToken);

        var result = await client.CallToolAsync(
            "search_digital_learning_materials",
            new Dictionary<string, object?>
            {
                ["topic"] = contextSummary,
                ["pageSize"] = 5
            },
            cancellationToken: cancellationToken);

        if (result.IsError == true)
        {
            throw new InvalidOperationException("The MCP education search tool returned an error.");
        }

        var responseText = result.Content
            .OfType<TextContentBlock>()
            .Select(block => block.Text)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(responseText))
        {
            throw new InvalidOperationException("The MCP education search tool returned no text content.");
        }

        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;
        var responseIsMock = root.GetProperty("isMock").GetBoolean();
        var data = root.GetProperty("data");
        if (data.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("The MCP education search tool returned an invalid data collection.");
        }

        return data.EnumerateArray()
            .Select(item => MapMaterial(item, responseIsMock))
            .ToArray();
    }

    private static EducationMaterial MapMaterial(JsonElement item, bool responseIsMock)
    {
        var source = item.GetProperty("source");
        var isMock = responseIsMock || source.GetProperty("isMock").GetBoolean();
        var sourceUrlValue = source.GetProperty("sourceUrl");
        var sourceUrl = sourceUrlValue.ValueKind == JsonValueKind.String
            && Uri.TryCreate(sourceUrlValue.GetString(), UriKind.Absolute, out var parsedUrl)
                ? parsedUrl
                : null;
        var keywords = item.TryGetProperty("appliesTo", out var appliesTo)
            && appliesTo.ValueKind == JsonValueKind.Array
                ? appliesTo.EnumerateArray()
                    .Where(value => value.ValueKind == JsonValueKind.String)
                    .Select(value => value.GetString()!)
                    .ToArray()
                : [];

        return new EducationMaterial(
            item.GetProperty("id").GetString() ?? string.Empty,
            item.GetProperty("category").GetString() ?? string.Empty,
            item.GetProperty("title").GetString() ?? string.Empty,
            item.GetProperty("summary").GetString() ?? string.Empty,
            Region: null,
            source.GetProperty("provider").GetString() ?? string.Empty,
            sourceUrl,
            IsOfficial: !isMock && sourceUrl is not null,
            IsMock: isMock,
            keywords);
    }
}
