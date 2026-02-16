namespace Reqnroll.LanguageServer.Models.FeatureCsParser;

public record FeatureNode
{
    public required string FeatureName { get; set; }
    public required string ClassName { get; set; }

    public IEnumerable<ScenarioNode> ScenarioNodes { get; set; } = new List<ScenarioNode>();
}