using Microsoft.TestPlatform.VsTestConsole.TranslationLayer;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Reqnroll.LanguageServer.Services.TestRunning;

// Runs tests for any VSTest-compatible framework (xUnit, NUnit, MSTest, ...) via
// Microsoft.TestPlatform.TranslationLayer's VsTestConsoleWrapper, which drives a
// bundled vstest.console.exe using its own internal process management (not
// Process.Start invoked by this code). vstest.console auto-discovers whichever
// test adapter (xunit.runner.visualstudio, NUnit3TestAdapter, MSTest adapter, ...)
// is deployed alongside the target assembly, so a single implementation suffices.
public sealed class VsTestConsoleTestRunner : IVsTestRunner
{
    private readonly VsCodeOutputLogger _logger;

    public VsTestConsoleTestRunner(VsCodeOutputLogger logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<DiscoveredTestCase>> DiscoverTestsAsync(string assemblyPath, CancellationToken cancellationToken = default)
    {
        var vstestConsolePath = GetVsTestConsolePath();
        if (vstestConsolePath is null)
        {
            return Task.FromResult<IReadOnlyList<DiscoveredTestCase>>(Array.Empty<DiscoveredTestCase>());
        }

        var fullAssemblyPath = Path.GetFullPath(assemblyPath);
        var wrapper = new VsTestConsoleWrapper(vstestConsolePath);

        try
        {
            var discoveryHandler = new DiscoveryHandler(_logger);
            wrapper.DiscoverTests(new[] { fullAssemblyPath }, null, null, discoveryHandler);
            discoveryHandler.Completed.Wait(cancellationToken);

            IReadOnlyList<DiscoveredTestCase> discovered = discoveryHandler.DiscoveredTestCases
                .Select(testCase => new DiscoveredTestCase
                {
                    FullyQualifiedName = testCase.FullyQualifiedName,
                    DisplayName = testCase.DisplayName,
                    TestCase = testCase,
                })
                .ToList();

            return Task.FromResult(discovered);
        }
        finally
        {
            wrapper.EndSession();
        }
    }

    public Task<IReadOnlyList<TestExecutionResult>> RunTestsAsync(
        string assemblyPath,
        IReadOnlyList<DiscoveredTestCase> testCases,
        Action<TestExecutionResult>? onTestCompleted = null,
        CancellationToken cancellationToken = default)
    {
        if (testCases.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<TestExecutionResult>>(Array.Empty<TestExecutionResult>());
        }

        var vstestConsolePath = GetVsTestConsolePath();
        if (vstestConsolePath is null)
        {
            return Task.FromResult<IReadOnlyList<TestExecutionResult>>(testCases.Select(x=>new TestExecutionResult
            {
                DisplayName = x.DisplayName,
                Outcome = VsTestOutcome.None,
                FullyQualifiedName = x.FullyQualifiedName,
                Output = string.Empty
            }).ToList());
        }

        var wrapper = new VsTestConsoleWrapper(vstestConsolePath);

        try
        {
            var runHandler = new RunHandler(_logger, onTestCompleted);
            wrapper.RunTests(testCases.Select(testCase => testCase.TestCase), null, runHandler);
            runHandler.Completed.Wait(cancellationToken);

            IReadOnlyList<TestExecutionResult> results = runHandler.Results
                .Select(ToTestExecutionResult)
                .ToList();

            return Task.FromResult(results);
        }
        finally
        {
            wrapper.EndSession();
        }
    }

    private static TestExecutionResult ToTestExecutionResult(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult result) => new()
    {
        FullyQualifiedName = result.TestCase.FullyQualifiedName,
        DisplayName = result.TestCase.DisplayName,
        Outcome = MapOutcome(result.Outcome),
        Output = BuildOutput(result),
    };

    private string? GetVsTestConsolePath()
    {
        var vstestConsolePath = Path.Combine(AppContext.BaseDirectory, "vstest.console", "vstest.console.exe");
        if (!File.Exists(vstestConsolePath))
        {
            _logger.LogError($"Could not locate the bundled vstest.console.exe at '{vstestConsolePath}'.");
            return null;
        }

        return vstestConsolePath;
    }

    private static VsTestOutcome MapOutcome(TestOutcome outcome) => outcome switch
    {
        TestOutcome.Passed => VsTestOutcome.Passed,
        TestOutcome.Failed => VsTestOutcome.Failed,
        TestOutcome.Skipped => VsTestOutcome.Skipped,
        _ => VsTestOutcome.None,
    };

    private static string? BuildOutput(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult result)
    {
        if (result.Outcome == TestOutcome.Failed)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                parts.Add(result.ErrorMessage);
            }
            if (!string.IsNullOrEmpty(result.ErrorStackTrace))
            {
                parts.Add(result.ErrorStackTrace);
            }
            return parts.Count > 0 ? string.Join(Environment.NewLine, parts) : null;
        }

        var standardOutput = result.Messages
            .Where(message => message.Category == Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResultMessage.StandardOutCategory)
            .Select(message => message.Text)
            .ToList();

        return standardOutput.Count > 0 ? string.Join(Environment.NewLine, standardOutput) : null;
    }

    private sealed class DiscoveryHandler(VsCodeOutputLogger logger) : ITestDiscoveryEventsHandler2
    {
        public List<TestCase> DiscoveredTestCases { get; } = new();

        public ManualResetEventSlim Completed { get; } = new(initialState: false);

        public void HandleDiscoveredTests(IEnumerable<TestCase>? discoveredTestCases)
        {
            if (discoveredTestCases is not null)
            {
                DiscoveredTestCases.AddRange(discoveredTestCases);
            }
        }

        public void HandleDiscoveryComplete(DiscoveryCompleteEventArgs discoveryCompleteEventArgs, IEnumerable<TestCase>? lastChunk)
        {
            if (lastChunk is not null)
            {
                DiscoveredTestCases.AddRange(lastChunk);
            }

            Completed.Set();
        }

        public void HandleLogMessage(TestMessageLevel level, string? message)
        {
            if (string.IsNullOrEmpty(message)) return;

            switch (level)
            {
                case TestMessageLevel.Informational:
                    logger.LogInfo(message);
                    break;
                case TestMessageLevel.Warning:
                    logger.LogWarning(message);
                    break;
                case TestMessageLevel.Error:
                    logger.LogError(message);
                    break;
                default:
                    logger.LogInfo($"[{level.ToString()}]" + message);
                    break;
            }
        }

        public void HandleRawMessage(string rawMessage)
        {
        }
    }

    private sealed class RunHandler(VsCodeOutputLogger logger, Action<TestExecutionResult>? onTestCompleted) : ITestRunEventsHandler
    {
        public List<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> Results { get; } = new();

        public ManualResetEventSlim Completed { get; } = new(initialState: false);

        public void HandleTestRunStatsChange(TestRunChangedEventArgs? testRunChangedArgs)
        {
            if (testRunChangedArgs?.NewTestResults is not null)
            {
                foreach (var result in testRunChangedArgs.NewTestResults)
                {
                    Results.Add(result);
                    onTestCompleted?.Invoke(ToTestExecutionResult(result));
                }
            }
        }

        public void HandleTestRunComplete(TestRunCompleteEventArgs testRunCompleteArgs, TestRunChangedEventArgs? lastChunkArgs, ICollection<AttachmentSet>? runContextAttachments, ICollection<string>? executorUris)
        {
            if (lastChunkArgs?.NewTestResults is not null)
            {
                foreach (var result in lastChunkArgs.NewTestResults)
                {
                    Results.Add(result);
                    onTestCompleted?.Invoke(ToTestExecutionResult(result));
                }
            }

            Completed.Set();
        }

        public int LaunchProcessWithDebuggerAttached(TestProcessStartInfo testProcessStartInfo) => -1;

        public void HandleLogMessage(TestMessageLevel level, string? message)
        {
            if (string.IsNullOrEmpty(message)) return;

            switch (level)
            {
                case TestMessageLevel.Informational:
                    logger.LogInfo(message);
                    break;
                case TestMessageLevel.Warning:
                    logger.LogWarning(message);
                    break;
                case TestMessageLevel.Error:
                    logger.LogError(message);
                    break;
                default:
                    logger.LogInfo($"[{level.ToString()}]" + message);
                    break;
            }
        }

        public void HandleRawMessage(string rawMessage)
        {
        }
    }
}
