using Newtonsoft.Json;

namespace Reqnroll.LanguageServer.Models.DotnetBuild;

public class StartBuildParams
{
    [JsonProperty("featureFileUri")]
    public string FeatureFileUri { get; set; } = string.Empty;
}
