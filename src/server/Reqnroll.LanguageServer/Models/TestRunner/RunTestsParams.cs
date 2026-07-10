using Newtonsoft.Json;

namespace Reqnroll.LanguageServer.Models.TestRunner;

public class RunTestsParams
{
    [JsonProperty("tests")]
    public List<TestInfo> Tests { get; set; } = new();

    // Optional client-generated correlation id. When set, the server streams a
    // "rotbarsch.reqnroll/testResult" notification per completed test (carrying this
    // same RunId) as soon as it finishes, instead of the client having to wait for the
    // whole batch to complete.
    [JsonProperty("runId")]
    public string? RunId { get; set; }
}