using Newtonsoft.Json;

namespace Reqnroll.LanguageServer.Models.TestDiscovery;

public record DiscoveredTest
{
    [JsonProperty("id")]
    public required string Id { get; init; }

    [JsonProperty("label")]
    public required string Label { get; init; }

    [JsonProperty("uri")]
    public required string Uri { get; init; }

    [JsonProperty("range")]
    public required TestRange Range { get; init; }

    [JsonProperty("children")]
    public List<DiscoveredTest>? Children { get; init; }
}