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

        var result = new List<TestResult>();
        foreach (var test in request.Tests)
        {
            _logger.LogInfo($"Running test {test.Id} from file {test.FilePath}");

            var csProjPath = ProjectFileFinder.GetProjectFileOfFeatureFile(test.FilePath);

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

            result.AddRange(await _dotnetTestService.RunTestAsync(csProjPath, test.Id));
        }

        return result;
    }
}