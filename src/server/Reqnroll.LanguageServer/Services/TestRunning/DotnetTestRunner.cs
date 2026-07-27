using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using TestOutcome = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome;
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
        var framework = GetFramework(csProjFilePath);

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
        await File.WriteAllTextAsync(runSettingsPath, BuildRunSettings(tests, framework), cancellationToken);

        try
        {
            var reported = new Dictionary<string, TestResult>();

            void ReportSingleTestResultToClient(TestInfo test, TestResultInfo info)
            {
                var message = !string.IsNullOrWhiteSpace(info.ErrorMessage)
                    ? info.ErrorMessage
                    : !string.IsNullOrWhiteSpace(info.Messages) ? info.Messages : null;
                var testResult = new TestResult
                {
                    Id = test.Id,
                    Passed = info.Outcome == TestOutcome.Passed,
                    Message = message,
                };
                reported[test.Id] = testResult;
                onTestCompleted?.Invoke(testResult);
            }

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
            var combinedOutput = new StringBuilder();

            process.OutputDataReceived += (o, s) =>
            {
                if (string.IsNullOrEmpty(s.Data)) return;

                combinedOutput.AppendLine(s.Data);
                _logger.LogInfo($"[dotnet test] {s.Data}");

                // Such a message only appears once per test, indicating its result
                if (s.Data.StartsWith($"[{MinimalConsoleLogger.LoggerName}]"))
                {
                    var json = s.Data.Split($"[{MinimalConsoleLogger.LoggerName}]").Skip(1).SingleOrDefault();
                    if (string.IsNullOrEmpty(json)) return;

                    var info = JsonSerializer.Deserialize<TestResultInfo>(json)!;
                    
                    var test = MatchTestFromTestResult(tests, info, reported.Keys);
                    if (test is not null)
                    {
                        ReportSingleTestResultToClient(test, info);
                    }
                }
            };

            process.ErrorDataReceived += (_, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    combinedOutput.AppendLine(args.Data);
                    _logger.LogWarning($"[dotnet test] {args.Data}");
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(cancellationToken);

            var unreported = tests.Where(t => !reported.ContainsKey(t.Id)).ToList();
            if (unreported.Count > 0)
            {
                var failMessage = combinedOutput.Length > 0
                    ? combinedOutput.ToString()
                    : $"dotnet test exited with code {process.ExitCode} without reporting a result for this test.";
                foreach (var test in unreported)
                {
                    var failResult = new TestResult
                    {
                        Id = test.Id,
                        Passed = false,
                        Message = failMessage,
                    };
                    reported[test.Id] = failResult;
                    onTestCompleted?.Invoke(failResult);
                }
            }

            return tests.Select(t => reported[t.Id]).ToList();
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

    private TestFramework GetFramework(string csProjFilePath)
    {
        XDocument csproj;
        try
        {
            csproj = XDocument.Load(csProjFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Could not read {csProjFilePath} to detect test framework: {ex.Message}");
            return TestFramework.MsTest;
        }

        var packageRefs = csproj.Descendants()
            .Where(e => e.Name.LocalName == "PackageReference")
            .Select(e => (e.Attribute("Include") ?? e.Attribute("include"))?.Value ?? string.Empty)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (packageRefs.Any(p => p.StartsWith("xunit", StringComparison.OrdinalIgnoreCase)))
            return TestFramework.XUnit;

        if (packageRefs.Any(p => p.StartsWith("NUnit", StringComparison.OrdinalIgnoreCase)))
            return TestFramework.NUnit;

        return TestFramework.MsTest;
    }

    private static string BuildRunSettings(IReadOnlyList<TestInfo> tests, TestFramework framework)
    {
        var runSettings = new XElement("RunSettings",
            new XElement("RunConfiguration",
                new XElement("TestCaseFilter",
                    string.Join("|", tests.Select(t=>GetTestAddress(t, framework)))
                    )
                )
            );

        return new XDocument(runSettings).ToString();
    }
    
    private static TestInfo? MatchTestFromTestResult(IReadOnlyList<TestInfo> tests, TestResultInfo info, IEnumerable<string> alreadyReportedIds)
    {
        var alreadyReported = new HashSet<string>(alreadyReportedIds);
        var fqn = StripMethodParameters(info.FullyQualifiedName ?? string.Empty);
        var extractedPickleIndex = ExtractPickleIndex(info.DisplayName ?? string.Empty);

        // Direct match for simple (non-parameterized) tests
        var direct = tests.FirstOrDefault(t =>
            !alreadyReported.Contains(t.Id) &&
            t.PickleIndex is null &&
            string.Equals(t.Id, fqn, StringComparison.Ordinal));
        if (direct is not null) return direct;

        // Parameterized: match by ParentId + PickleIndex from display name
        if (extractedPickleIndex.HasValue)
        {
            var paramMatch = tests.FirstOrDefault(t =>
                !alreadyReported.Contains(t.Id) &&
                t.PickleIndex == extractedPickleIndex &&
                string.Equals(t.ParentId, fqn, StringComparison.Ordinal));
            if (paramMatch is not null) return paramMatch;
        }

        return null;
    }

    private static int? ExtractPickleIndex(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return null;

        // xUnit: named parameter __pickleIndex: "N"
        var xunitMatch = Regex.Match(displayName, @"__pickleIndex\s*:\s*""(\d+)""");
        if (xunitMatch.Success && int.TryParse(xunitMatch.Groups[1].Value, out var xunitIndex))
            return xunitIndex;

        // MSTest/NUnit: pickle index is last (or second-to-last before null) positional parameter
        var positionalMatch = Regex.Match(displayName, @"\(([^,]+),([^,]+),([^,]+),""?(\d+)""?");
        if (positionalMatch.Success && int.TryParse(positionalMatch.Groups[^1].Value, out var positionalIndex))
            return positionalIndex;

        return null;
    }

    private static string StripMethodParameters(string fullyQualifiedName)
    {
        var parenIndex = fullyQualifiedName.IndexOf('(');
        return parenIndex >= 0 ? fullyQualifiedName[..parenIndex] : fullyQualifiedName;
    }

    private static string GetTestAddress(TestInfo arg, TestFramework framework)
    {
        /*
        <TestCaseFilter>
            <!-- MSTest -->
            FullyQualifiedName~Features.SubFolder.AFeatureWithBooleansInASubfolderFeature.FunWithBool &amp; Name~,0 
            
            <!-- xUnit -->
            FullyQualifiedName~Features.SubFolder.AFeatureWithBooleansInASubfolderFeature.FunWithBool &amp; DisplayName~pickleIndex: "0"
        </TestCaseFilter>
         */

        // No action needed for simple tests (TestMethod, Fact,...)
        if (arg.PickleIndex is null) return arg.Id;

        // DataRow/Theory tests on the other hand require some extra attention.

        switch (framework)
        {
            case TestFramework.MsTest:
                return $"FullyQualifiedName~{arg.ParentId} & Name~,{arg.PickleIndex}";
            case TestFramework.XUnit:
                return $"FullyQualifiedName~{arg.ParentId} & DisplayName~pickleIndex: \"{arg.PickleIndex}\"";
            case TestFramework.NUnit:
                return $"{arg.ParentId} & FullyQualifiedName~,\"{arg.PickleIndex}\",null";
            default:
                throw new ArgumentOutOfRangeException(nameof(framework), framework, null);
        }
    }
}

enum TestFramework
{
    MsTest,
    NUnit,
    XUnit
}