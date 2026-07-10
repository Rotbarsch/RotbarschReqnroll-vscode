namespace Reqnroll.LanguageServer.Services.TestRunning;

public enum VsTestOutcome
{
    None,
    Passed,
    Failed,
    Skipped,
}

// The outcome of running a single discovered test case, decoupled from the VSTest
// ObjectModel's own TestResult/TestOutcome types.
public sealed record TestExecutionResult
{
    public required string FullyQualifiedName { get; init; }
    public required string DisplayName { get; init; }
    public required VsTestOutcome Outcome { get; init; }
    public string? Output { get; init; }
}
