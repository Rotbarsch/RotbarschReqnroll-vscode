using Newtonsoft.Json;

namespace Reqnroll.LanguageServer.Models.TestDiscovery;

public record TestRange
{
    [JsonProperty("startLine")]
    public required int StartLine { get; init; }

    [JsonProperty("startCharacter")]
    public required int StartCharacter { get; init; }

    [JsonProperty("endLine")]
    public required int EndLine { get; init; }

    [JsonProperty("endCharacter")]
    public required int EndCharacter { get; init; }
}