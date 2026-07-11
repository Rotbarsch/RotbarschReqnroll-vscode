using Reqnroll.LanguageServer.Helpers;
using Reqnroll.LanguageServer.Models.TestRunner;
using Reqnroll.LanguageServer.Services.TestRunning;

namespace Reqnroll.LanguageServer.Services;

public class ReqnrollTestRunnerService
{
    private readonly VsCodeOutputLogger _logger;
    private readonly IDotnetTestRunner _testRunner;

    public ReqnrollTestRunnerService(VsCodeOutputLogger logger, IDotnetTestRunner testRunner)
    {
        _logger = logger;
        _testRunner = testRunner;
    }

    public Task<List<TestResult>> HandleRunTestsRequestAsync(RunTestsParams request, CancellationToken cancellationToken)
        => HandleRunTestsRequestAsync(request, onTestCompleted: null, cancellationToken);

    public async Task<List<TestResult>> HandleRunTestsRequestAsync(RunTestsParams request, Action<TestResult>? onTestCompleted, CancellationToken cancellationToken)
    {
        _logger.LogInfo($"runTests handler invoked with {request.Tests.Count} test(s)");

        // Group tests by project file
        var testsByProject = new Dictionary<string, List<TestInfo>>();
        var result = new List<TestResult>();

        void Report(TestResult testResult)
        {
            result.Add(testResult);
            onTestCompleted?.Invoke(testResult);
        }

        foreach (var test in request.Tests)
        {
            _logger.LogInfo($"Test {test.Id} from file {test.FilePath}");

            var csProjPath = BuildableFileFinder.GetBuildableFileOfReferenceFile(test.FilePath, forceCsProj: true);

            if (string.IsNullOrEmpty(csProjPath))
            {
                Report(new TestResult
                {
                    Id = test.Id,
                    Message = "Unable to find the project file for the test. Make sure the feature file is part of a project and try again.",
                    Line = 0,
                    Passed = false,
                });
                continue;
            }

            if (!testsByProject.TryGetValue(csProjPath, out var list))
            {
                list = new List<TestInfo>();
                testsByProject[csProjPath] = list;
            }
            list.Add(test);
        }

        // Run once per project, covering all of that project's requested tests via `dotnet test`.
        var tasks = testsByProject.Select(kvp => RunTestsForProjectAsync(kvp.Key, kvp.Value, onTestCompleted, cancellationToken));
        var resultArrays = await Task.WhenAll(tasks);
        result.AddRange(resultArrays.SelectMany(r => r));

        return result;
    }

    private async Task<IReadOnlyList<TestResult>> RunTestsForProjectAsync(string csProjFilePath, List<TestInfo> tests, Action<TestResult>? onTestCompleted, CancellationToken cancellationToken)
    {
        var dllPath = ProjectOutputDllFinder.GetOutputDllPath(csProjFilePath);
        if (dllPath is null || !File.Exists(dllPath))
        {
            return Report(tests, onTestCompleted, "Could not find output DLL for the project. Make sure the project builds successfully.");
        }

        try
        {
            return await _testRunner.RunTestsAsync(csProjFilePath, tests, onTestCompleted, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Running tests failed for '{csProjFilePath}': {ex.Message}");
            return Report(tests, onTestCompleted, $"Running tests failed: {ex.Message}");
        }
    }

    private static List<TestResult> Report(List<TestInfo> tests, Action<TestResult>? onTestCompleted, string message)
    {
        var results = tests.Select(t => new TestResult
        {
            Id = t.Id,
            Message = message,
            Line = 0,
            Passed = false,
        }).ToList();

        foreach (var r in results) onTestCompleted?.Invoke(r);

        return results;
    }
}
