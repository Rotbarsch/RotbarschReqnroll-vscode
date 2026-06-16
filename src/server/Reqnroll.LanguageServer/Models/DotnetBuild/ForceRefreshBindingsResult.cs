using Newtonsoft.Json;

namespace Reqnroll.LanguageServer.Models.DotnetBuild;

public class ForceRefreshBindingsResult
{
    [JsonProperty("bindingCount")]
    public int BindingCount { get; set; }
}
