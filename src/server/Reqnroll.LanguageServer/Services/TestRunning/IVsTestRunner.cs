namespace Reqnroll.LanguageServer.Services.TestRunning;

// Abstraction over vstest.console-based test discovery/execution, so consumers
// (ReqnrollTestRunnerService) don't depend on the VSTest ObjectModel/TranslationLayer directly.
public interface IVsTestRunner
{
    Task<IReadOnlyList<DiscoveredTestCase>> DiscoverTestsAsync(string assemblyPath, CancellationToken cancellationToken = default);

    // onTestCompleted (if provided) is invoked as soon as each individual test case finishes,
    // before the whole batch completes - allowing callers to stream results incrementally.
    Task<IReadOnlyList<TestExecutionResult>> RunTestsAsync(
        string assemblyPath,
        IReadOnlyList<DiscoveredTestCase> testCases,
        Action<TestExecutionResult>? onTestCompleted = null,
        CancellationToken cancellationToken = default);
}
