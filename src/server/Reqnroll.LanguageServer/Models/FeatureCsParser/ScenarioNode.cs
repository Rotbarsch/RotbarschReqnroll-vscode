namespace Reqnroll.LanguageServer.Models.FeatureCsParser;

public record ScenarioNode
{
    public required string ScenarioName { get; set; }
    public required string MethodName { get; set; }
}