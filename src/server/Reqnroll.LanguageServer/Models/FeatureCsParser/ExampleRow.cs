namespace Reqnroll.LanguageServer.Models.FeatureCsParser;

public record ExampleRow
{
    public required string Arguments { get; set; }
    public required int PickleIndex { get; set; }
}
