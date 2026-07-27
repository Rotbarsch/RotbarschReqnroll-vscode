using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Newtonsoft.Json;
using Reqnroll.LanguageServer.Models.TestRunner;
using Rotbarsch.Reqnroll.VsTestConsoleLogger;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Reqnroll.LanguageServer.Services.TestRunning;

// Drives `dotnet test` (via Process.Start) against a specific project, filtering down to the
// requested tests with a `--filter` expression and a temporary .runsettings file (which also
// records the requested fully qualified names as TestRunParameters for traceability). Results
// are obtained purely by parsing the live console logger output as tests complete -- there is
// intentionally no TRX logger/file involved, so each test result comes from exactly one source
// of truth (the streamed console output) instead of a separate "authoritative" TRX pass at the
// end. The console logger writes each test's result block atomically, so this is reliable even
// when the test framework runs tests in parallel.
public sealed class DotnetTestRunner : IDotnetTestRunner
{
    private readonly VsCodeOutputLogger _logger;

    // Matches the per-test result line the vstest console logger prints as each test finishes,
    // e.g. "  Passed TestNamespace.TestClass.TestMethod [12 ms]". The assembly-level summary
    // line ("Passed!  - Failed: 0, Passed: 3, ...") uses "Passed!"/"Failed!" (with an
    // exclamation mark immediately after the word) so it never matches this pattern.
    private static readonly Regex ResultLineRegex = new(
        @"^\s*(Passed|Failed|Skipped)\s+(.+?)\s*\[[^\[\]]*\]\s*$",
        RegexOptions.Compiled);

    public DotnetTestRunner(VsCodeOutputLogger logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<TestResult>> RunTestsAsync(
        string csProjFilePath,
        IReadOnlyList<TestInfo> tests,
        Action<TestResult>? onTestCompleted = null,
        CancellationToken cancellationToken = default)
    {
        if (tests.Count == 0)
        {
            return Array.Empty<TestResult>();
        }

        var baseDir = Path.Combine(Path.GetTempPath(), "rotbarsch.reqnrollvscode");
        if (!Directory.Exists(baseDir))
        {
            Directory.CreateDirectory(baseDir);
        }

        var runSettingsDir = Path.Combine(baseDir, "runsettings");
        if (!Directory.Exists(runSettingsDir))
        {
            Directory.CreateDirectory(runSettingsDir);
        }

        var runSettingsPath = Path.Combine(runSettingsDir, $"rotbarsch.reqnroll_{Guid.NewGuid():N}.runsettings");
        await File.WriteAllTextAsync(runSettingsPath, BuildRunSettings(tests), cancellationToken);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(csProjFilePath),
                EnvironmentVariables =
                {
                    ["DOTNET_CLI_UI_LANGUAGE"] = "en",
                    ["PreferredUILang"] = "en-US"
                }
            };
            
            psi.ArgumentList.Add("test");
            psi.ArgumentList.Add(csProjFilePath);
            psi.ArgumentList.Add("--no-restore");
            psi.ArgumentList.Add("--no-build");
            psi.ArgumentList.Add("--settings");
            psi.ArgumentList.Add(runSettingsPath);
            psi.ArgumentList.Add("--test-adapter-path");
            psi.ArgumentList.Add(AppContext.BaseDirectory);
            psi.ArgumentList.Add("--logger");
            psi.ArgumentList.Add(MinimalConsoleLogger.LoggerName);

            _logger.LogInfo("Running dotnet " + string.Join(" ", psi.ArgumentList));

            using var process = new Process { StartInfo = psi };

            var results = new List<TestResultInfo>();
            process.OutputDataReceived += (o,s) =>
            {
                if (string.IsNullOrEmpty(s.Data)) return;

                // Such a message only appears once per test, indicating its result
                if (s.Data.StartsWith($"[{MinimalConsoleLogger.LoggerName}]"))
                {
                    var json = s.Data.Split($"[{MinimalConsoleLogger.LoggerName}]").Skip(1).SingleOrDefault();
                    if (string.IsNullOrEmpty(json)) return;

                    results.Add(JsonSerializer.Deserialize<TestResultInfo>(json)!);

                    // TODO notify client
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(cancellationToken);

            // TODO return all results
            return [];
        }
        finally
        {
            try
            {
                File.Delete(runSettingsPath);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }

    private static string BuildRunSettings(IReadOnlyList<TestInfo> tests)
    {
        var runSettings = new XElement("RunSettings",
            new XElement("RunConfiguration",
                new XElement("TestCaseFilter",
                    string.Join("|", tests.Select(GetTestAddress))
                    )
                )
            );

        return new XDocument(runSettings).ToString();
    }

    private static string GetTestAddress(TestInfo arg)
    {
        /*
        <TestCaseFilter>
            <!-- NUnit -->
            FullyQualifiedName~Features.SubFolder.AFeatureWithBooleansInASubfolderFeature.FunWithBool &amp; Name~,"0",null
            <!-- MSTest -->
            FullyQualifiedName~Features.SubFolder.AFeatureWithBooleansInASubfolderFeature.FunWithBool &amp; Name~,0 
            
            <!-- xUnit -->
            FullyQualifiedName~Features.SubFolder.AFeatureWithBooleansInASubfolderFeature.FunWithBool &amp; DisplayName~pickleIndex: "0"
        </TestCaseFilter>
         */

        // No action needed for simple tests (TestMethod, Fact,...)
        if (arg.PickleIndex is null) return arg.Id;

        // DataRow/Theory tests on the other hand require some extra attention.
        var msTestFilter = $"FullyQualifiedName~{arg.ParentId} & Name~,{arg.PickleIndex}";
        var nUnitFilter = $"FullyQualifiedName~{arg.ParentId} & Name~,\"{arg.PickleIndex}\",null";
        var xUnitFilter = $"FullyQualifiedName~{arg.ParentId} &amp; DisplayName~pickleIndex: \"{arg.PickleIndex}\"";

        return $"(({msTestFilter})|({nUnitFilter})|({xUnitFilter}))";
    }
}
