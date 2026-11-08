using System.Xml.Linq;
using Reqnroll.LanguageServer.Models.TestRunner;

namespace Reqnroll.LanguageServer.Services.TestRunning;

// Picks the right test runner per project: Microsoft.Testing.Platform (MTP) test projects (e.g.
// xunit.v3 without the VSTest bridge) can no longer be run through `dotnet test`'s VSTest mode on
// .NET 10+ SDKs, so they're routed to the MTP server-mode runner instead of the classic
// `dotnet test` + VSTest console logger runner used by everything else (xUnit v2, NUnit, MSTest).
public sealed class TestRunnerDispatcher : IDotnetTestRunner
{
    private readonly DotnetTestRunner _vsTestRunner;
    private readonly MtpTestRunner _mtpTestRunner;

    public TestRunnerDispatcher(DotnetTestRunner vsTestRunner, MtpTestRunner mtpTestRunner)
    {
        _vsTestRunner = vsTestRunner;
        _mtpTestRunner = mtpTestRunner;
    }

    public Task<IReadOnlyList<TestResult>> RunTestsAsync(
        string csProjFilePath,
        IReadOnlyList<TestInfo> tests,
        Action<TestResult>? onTestCompleted = null,
        CancellationToken cancellationToken = default)
    {
        var runner = IsMicrosoftTestingPlatformProject(csProjFilePath) ? (IDotnetTestRunner)_mtpTestRunner : _vsTestRunner;
        return runner.RunTestsAsync(csProjFilePath, tests, onTestCompleted, cancellationToken);
    }

    private static bool IsMicrosoftTestingPlatformProject(string csProjFilePath)
    {
        try
        {
            var csproj = XDocument.Load(csProjFilePath);

            var packageRefs = csproj.Descendants()
                .Where(e => e.Name.LocalName == "PackageReference")
                .Select(e => (e.Attribute("Include") ?? e.Attribute("include"))?.Value ?? string.Empty);

            if (packageRefs.Any(p => string.Equals(p, "xunit.v3", StringComparison.OrdinalIgnoreCase)))
                return true;

            var useMtpRunner = csproj.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "UseMicrosoftTestingPlatformRunner")?.Value;

            return bool.TryParse(useMtpRunner, out var flag) && flag;
        }
        catch
        {
            return false;
        }
    }
}
