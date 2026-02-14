namespace Reqnroll.LanguageServer.Models.TestRunner;

public record TestResult
{
    public required string Id { get; init; }
    public bool Passed { get; init; }
    public string? Message { get; init; }
    public int? Line { get; init; }
}