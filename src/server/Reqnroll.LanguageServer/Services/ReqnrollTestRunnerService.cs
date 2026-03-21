using Reqnroll.LanguageServer.Helpers;
using Reqnroll.LanguageServer.Models.TestRunner;

namespace Reqnroll.LanguageServer.Services;

public class ReqnrollTestRunnerService
{
    private readonly VsCodeOutputLogger _logger;
    private readonly DotnetTestService _dotnetTestService;

    public ReqnrollTestRunnerService(VsCodeOutputLogger logger, DotnetTestService dotnetTestService)
    {
        _logger = logger;
        _dotnetTestService = dotnetTestService;
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

            var csProjPath = BuildableFileFinder.GetBuildableFileOfReferenceFile(test.FilePath);

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

            if (!testsByProject.ContainsKey(csProjPath))
                testsByProject[csProjPath] = new List<TestInfo>();
            testsByProject[csProjPath].Add(test);
        }

        // Run one dotnet test per project with a combined filter
        var tasks = testsByProject.Select(async kvp =>
            await _dotnetTestService.RunTestsBatchAsync(kvp.Key, kvp.Value));
        var resultArrays = await Task.WhenAll(tasks);
        result.AddRange(resultArrays.SelectMany(r => r));

        return result;
    }
}