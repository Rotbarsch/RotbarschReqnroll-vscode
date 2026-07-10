using System.Text.RegularExpressions;
using Reqnroll.LanguageServer.Helpers;
using Reqnroll.LanguageServer.Models.TestRunner;
using Reqnroll.LanguageServer.Services.TestRunning;

namespace Reqnroll.LanguageServer.Services;

public class ReqnrollTestRunnerService
{
    private readonly VsCodeOutputLogger _logger;
    private readonly IVsTestRunner _testRunner;

    public ReqnrollTestRunnerService(VsCodeOutputLogger logger, IVsTestRunner testRunner)
    {
        _logger = logger;
        _testRunner = testRunner;
    }

    public async Task<List<TestResult>> HandleRunTestsRequestAsync(RunTestsParams request, CancellationToken cancellationToken)
    {
        _logger.LogInfo($"runTests handler invoked with {request.Tests.Count} test(s)");

        // Group tests by project file
        var testsByProject = new Dictionary<string, List<TestInfo>>();
        var result = new List<TestResult>();

        foreach (var test in request.Tests)
        {
            _logger.LogInfo($"Test {test.Id} from file {test.FilePath}");

            var csProjPath = BuildableFileFinder.GetBuildableFileOfReferenceFile(test.FilePath, forceCsProj: true);

            if (string.IsNullOrEmpty(csProjPath))
            {
                result.Add(new TestResult
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

        // Discover + run once per project, covering all of that project's requested tests.
        var tasks = testsByProject.Select(kvp => RunTestsForProjectAsync(kvp.Key, kvp.Value, cancellationToken));
        var resultArrays = await Task.WhenAll(tasks);
        result.AddRange(resultArrays.SelectMany(r => r));

        return result;
    }

    private async Task<List<TestResult>> RunTestsForProjectAsync(string csProjFilePath, List<TestInfo> tests, CancellationToken cancellationToken)
    {
        var dllPath = ProjectOutputDllFinder.GetOutputDllPath(csProjFilePath);
        if (dllPath is null || !File.Exists(dllPath))
        {
            return tests.Select(t => new TestResult
            {
                Id = t.Id,
                Message = "Could not find output DLL for the project. Make sure the project builds successfully.",
                Line = 0,
                Passed = false,
            }).ToList();
        }

        IReadOnlyList<DiscoveredTestCase> discovered;
        try
        {
            discovered = await _testRunner.DiscoverTestsAsync(dllPath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Test discovery failed for '{dllPath}': {ex.Message}");
            return tests.Select(t => new TestResult
            {
                Id = t.Id,
                Message = $"Test discovery failed: {ex.Message}",
                Line = 0,
                Passed = false,
            }).ToList();
        }

        if (discovered.Count == 0)
        {
            return tests.Select(t => new TestResult
            {
                Id = t.Id,
                Message = "No tests were discovered in the compiled assembly. Make sure the project builds successfully.",
                Line = 0,
                Passed = false,
            }).ToList();
        }

        // Group discovered cases by their base method name (parameters stripped) so that
        // scenario outline rows sharing the same method can be told apart by pickle index below.
        var discoveredByBaseMethod = discovered
            .GroupBy(d => StripMethodParameters(d.FullyQualifiedName))
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<TestResult>();
        var matchedTestCases = new List<DiscoveredTestCase>();
        var testInfoByTestCase = new Dictionary<(string FullyQualifiedName, string DisplayName), TestInfo>();

        foreach (var test in tests)
        {
            var baseMethod = test.ParentId ?? test.Id;

            if (!discoveredByBaseMethod.TryGetValue(baseMethod, out var candidates) || candidates.Count == 0)
            {
                result.Add(new TestResult
                {
                    Id = test.Id,
                    Message = "Could not find a matching test in the compiled assembly. Make sure the project has been built with the latest changes.",
                    Line = 0,
                    Passed = false,
                });
                continue;
            }

            DiscoveredTestCase? matched;

            if (test.PickleIndex.HasValue)
            {
                matched = candidates.FirstOrDefault(c => TestNameContainsPickleIndex(c.DisplayName, test.PickleIndex.Value))
                          ?? (candidates.Count == 1 ? candidates[0] : null);
            }
            else
            {
                matched = candidates[0];
            }

            if (matched is null)
            {
                result.Add(new TestResult
                {
                    Id = test.Id,
                    Message = "Could not find a matching test in the compiled assembly. Make sure the project has been built with the latest changes.",
                    Line = 0,
                    Passed = false,
                });
                continue;
            }

            matchedTestCases.Add(matched);
            testInfoByTestCase[(matched.FullyQualifiedName, matched.DisplayName)] = test;
        }

        if (matchedTestCases.Count == 0)
        {
            return result;
        }

        IReadOnlyList<TestExecutionResult> executionResults;
        try
        {
            executionResults = await _testRunner.RunTestsAsync(dllPath, matchedTestCases, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Running tests failed for '{dllPath}': {ex.Message}");
            foreach (var test in testInfoByTestCase.Values)
            {
                result.Add(new TestResult
                {
                    Id = test.Id,
                    Message = $"Running tests failed: {ex.Message}",
                    Line = 0,
                    Passed = false,
                });
            }
            return result;
        }

        foreach (var executionResult in executionResults)
        {
            var key = (executionResult.FullyQualifiedName, executionResult.DisplayName);
            if (!testInfoByTestCase.TryGetValue(key, out var test))
            {
                continue;
            }

            result.Add(new TestResult
            {
                Id = test.Id,
                Message = executionResult.Output,
                Line = 0,
                Passed = executionResult.Outcome == VsTestOutcome.Passed,
            });
        }

        return result;
    }

    private static string StripMethodParameters(string fullyQualifiedName)
    {
        var parenIndex = fullyQualifiedName.IndexOf('(');
        return parenIndex >= 0 ? fullyQualifiedName[..parenIndex] : fullyQualifiedName;
    }

    private static bool TestNameContainsPickleIndex(string displayName, int pickleIndex)
    {
        var idx = pickleIndex.ToString();

        // xUnit: named parameter __pickleIndex: "N"
        if (displayName.Contains($"__pickleIndex: \"{idx}\""))
            return true;

        // NUnit: positional params, pickle is second-to-last before null/array
        // e.g. FunWithBool("true","true","true","0",null)
        if (Regex.IsMatch(displayName, "\"" + Regex.Escape(idx) + "\"" + @"\s*,\s*(null|\[.*?\])\s*\)\s*$"))
            return true;

        // MSTest: pickle index is last value in display name
        // e.g. "Fun with bool(true,true,true,0)"
        if (Regex.IsMatch(displayName, @",\s*" + Regex.Escape(idx) + @"\s*\)\s*$"))
            return true;

        return false;
    }
}
