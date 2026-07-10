using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Reqnroll.LanguageServer.Services.TestRunning;

// A test case discovered inside a compiled test assembly by vstest.console, decoupled
// from the underlying VSTest ObjectModel type so callers outside this namespace don't
// need to reference it directly.
public sealed record DiscoveredTestCase
{
    public required string FullyQualifiedName { get; init; }
    public required string DisplayName { get; init; }

    // Kept so RunTestsAsync can hand the exact same TestCase instances back to
    // VsTestConsoleWrapper.RunTests without needing to re-discover or reconstruct them.
    internal TestCase TestCase { get; init; } = null!;
}
