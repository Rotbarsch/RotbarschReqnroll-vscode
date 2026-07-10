namespace Reqnroll.LanguageServer.Services.TestRunning;

// Abstraction over vstest.console-based test discovery/execution, so consumers
// (ReqnrollTestRunnerService) don't depend on the VSTest ObjectModel/TranslationLayer directly.
public interface IVsTestRunner
{
    Task<IReadOnlyList<DiscoveredTestCase>> DiscoverTestsAsync(string assemblyPath, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TestExecutionResult>> RunTestsAsync(string assemblyPath, IReadOnlyList<DiscoveredTestCase> testCases, CancellationToken cancellationToken = default);
}
