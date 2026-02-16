using Newtonsoft.Json;

namespace Reqnroll.LanguageServer.Models.TestRunner;

public record TestResult
{
    [JsonProperty("id")]
    public required string Id { get; init; }
    
    [JsonProperty("passed")]
    public bool Passed { get; init; }
    
    [JsonProperty("message")]
    public string? Message { get; init; }
    
    [JsonProperty("line")]
    public int? Line { get; init; }
}