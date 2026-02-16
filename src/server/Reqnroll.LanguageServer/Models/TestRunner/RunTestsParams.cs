using Newtonsoft.Json;

namespace Reqnroll.LanguageServer.Models.TestRunner;

public class RunTestsParams
{
    [JsonProperty("tests")]
    public List<TestInfo> Tests { get; set; } = new();
}