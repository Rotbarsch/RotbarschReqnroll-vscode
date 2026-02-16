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
}