using Newtonsoft.Json;

namespace Reqnroll.LanguageServer.Models.DotnetWatch;

public class StartBuildParams
{
    [JsonProperty("featureFileUri")]
    public string FeatureFileUri { get; set; } = string.Empty;
}
