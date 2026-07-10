using Newtonsoft.Json;

namespace Reqnroll.LanguageServer.Models.TestRunner;

// Sent as a "rotbarsch.reqnroll/testResult" notification for each individual test as soon
// as it finishes, so the client can update the UI incrementally instead of waiting for the
// whole runTests request/batch to complete.
public record TestResultNotification
{
    [JsonProperty("runId")]
    public required string RunId { get; init; }

    [JsonProperty("result")]
    public required TestResult Result { get; init; }
}
