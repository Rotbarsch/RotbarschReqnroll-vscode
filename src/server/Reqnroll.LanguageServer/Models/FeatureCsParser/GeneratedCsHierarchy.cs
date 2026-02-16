namespace Reqnroll.LanguageServer.Models.FeatureCsParser;

public record GeneratedCsHierarchy
{
    public IEnumerable<FeatureNode> FeatureNodes { get; set; } = new List<FeatureNode>();
    public required string Namespace { get; set; }
}