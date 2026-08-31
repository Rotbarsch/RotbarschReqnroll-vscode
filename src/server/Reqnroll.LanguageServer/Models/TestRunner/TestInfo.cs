using Newtonsoft.Json;

namespace Reqnroll.LanguageServer.Models.TestRunner;

public class TestInfo
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonProperty("parentId")]
    public string? ParentId { get; set; }

    [JsonProperty("pickleIndex")]
    public int? PickleIndex { get; set; }

    [JsonProperty("isContainer")]
    public bool IsContainer { get; set; }

    // The human-readable scenario title, as computed during test discovery. Several test
    // frameworks (xUnit, MSTest) render this title -- not the raw C# method name -- as the
    // test's display name in `dotnet test`'s console output, so it's needed to map a completed
    // test's console output line back to this TestInfo.
    [JsonProperty("label")]
    public string? Label { get; set; }

    // We hijack this field to have unescaped display names (relevant for MS Test filter construction)
    [JsonProperty("description")]
    public string? Description { get; set; }
}