using Newtonsoft.Json;

namespace Reqnroll.LanguageServer.Models.DotnetWatch;

public class BuildResult
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;

    [JsonProperty("projectFile")]
    public string? ProjectFile { get; set; }
}
