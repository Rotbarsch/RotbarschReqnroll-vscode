using Newtonsoft.Json;

namespace Reqnroll.LanguageServer.Models.DotnetBuild;

public class StartBuildParams
{
    [JsonProperty("referenceFileUri")]
    public string ReferenceFileUri { get; set; } = string.Empty;

    [JsonProperty("fullRebuild")]
    public bool FullRebuild { get; set; } = false;
}
