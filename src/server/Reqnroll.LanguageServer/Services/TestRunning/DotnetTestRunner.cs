using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Reqnroll.LanguageServer.Models.TestRunner;

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

        var runSettingsPath = Path.Combine(Path.GetTempPath(), $"rotbarsch.reqnroll_{Guid.NewGuid():N}.runsettings");
        File.WriteAllText(runSettingsPath, BuildRunSettings(tests));

        try
        {
            var reported = new Dictionary<string, TestResult>();

            void ReportOne(TestInfo test, bool passed, string? message)
            {
                var testResult = new TestResult
                {
                    Id = test.Id,
                    Message = message,
                    Line = 0,
                    Passed = passed,
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
            };

            // The console logger's output (e.g. "Passed"/"Failed"/"Skipped") is localized based
            // on the OS UI culture, which would break the regex-based parsing below on non-English
            // systems. Force English so parsing is reliable regardless of the user's OS language.
            psi.EnvironmentVariables["DOTNET_CLI_UI_LANGUAGE"] = "en";
            psi.EnvironmentVariables["PreferredUILang"] = "en-US";

            psi.ArgumentList.Add("test");
            psi.ArgumentList.Add(csProjFilePath);
            psi.ArgumentList.Add("--no-restore");
            psi.ArgumentList.Add("--no-build");
            psi.ArgumentList.Add("--settings");
            psi.ArgumentList.Add(runSettingsPath);
            psi.ArgumentList.Add("--logger");
            psi.ArgumentList.Add("console;verbosity=detailed");

            _logger.LogInfo("Running dotnet " + string.Join(" ", psi.ArgumentList));

            using var process = new Process { StartInfo = psi };
            var combinedOutput = new StringBuilder();

            // Accumulate the current test's result block (its outcome line plus any error
            // message/stack trace/standard output lines that follow) until the next test's
            // result line (or the end of the process) tells us the block is complete.
            string? pendingStatus = null;
            string? pendingDisplayName = null;
            var pendingOutput = new StringBuilder();

            void FlushPending()
            {
                if (pendingStatus is null || pendingDisplayName is null)
                {
                    return;
                }

                var test = MatchTest(tests, pendingDisplayName, reported.Keys);
                if (test is not null)
                {
                    ReportOne(test, passed: pendingStatus == "Passed", message: TrimOrNull(pendingOutput.ToString()));
                }

                pendingStatus = null;
                pendingDisplayName = null;
                pendingOutput.Clear();
            }

            process.OutputDataReceived += (_, args) =>
            {
                if (args.Data is null)
                {
                    return;
                }

                combinedOutput.AppendLine(args.Data);
                _logger.LogInfo($"[dotnet test] {args.Data}");

                var match = ResultLineRegex.Match(args.Data);
                if (match.Success)
                {
                    FlushPending();
                    pendingStatus = match.Groups[1].Value;
                    pendingDisplayName = match.Groups[2].Value.Trim();
                }
                else if (pendingStatus is not null && !string.IsNullOrWhiteSpace(args.Data))
                {
                    pendingOutput.AppendLine(args.Data.Trim());
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
            FlushPending();

            // Any requested test that never produced a recognizable result line (e.g. the
            // project failed to build, the filter matched nothing, or the output format
            // couldn't be parsed) is reported as a failure using whatever output we captured.
            var unreported = tests.Where(t => !reported.ContainsKey(t.Id)).ToList();
            if (unreported.Count > 0)
            {
                var message = combinedOutput.Length > 0
                    ? combinedOutput.ToString()
                    : $"dotnet test exited with code {process.ExitCode} without reporting a result for this test.";

                foreach (var test in unreported)
                {
                    ReportOne(test, passed: false, message: message);
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

    private static string BuildRunSettings(IReadOnlyList<TestInfo> tests)
    {
        // .runsettings has no native concept of "run exactly these tests" that works across
        // xUnit/NUnit/MSTest -- actual test selection is done via the --filter argument below.
        // The requested fully qualified names are still recorded here (as TestRunParameters)
        // so the run configuration is self-describing and traceable.
        var runSettings = new XElement("RunSettings",
            new XElement("RunConfiguration",
                new XElement("TestCaeFilter",
                    string.Join("|", tests.Select(t => t.Id))
                    )
                )
            );

        return new XDocument(runSettings).ToString();
    }
    
    // Matches a completed test's console display name back to the TestInfo the caller asked to
    // run. Several frameworks (xUnit, MSTest) use the scenario title -- not the raw method name
    // -- as the test's console display name, while others (NUnit) use the raw method name, so a
    // candidate is accepted if the stripped display name matches either.
    private static TestInfo? MatchTest(IReadOnlyList<TestInfo> tests, string displayName, ICollection<string> alreadyReportedIds)
    {
        var strippedDisplayName = StripMethodParameters(displayName);
        var extractedPickleIndex = ExtractPickleIndex(displayName);

        var candidates = tests
            .Where(t => 
                !alreadyReportedIds.Contains(t.Id) && 
                MatchTestByNameAndPickle(t, strippedDisplayName, extractedPickleIndex)
            
            )
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        if (candidates.Count == 1 && !candidates[0].PickleIndex.HasValue)
        {
            return candidates[0];
        }

        return candidates.FirstOrDefault(t => t.PickleIndex.HasValue && TestNameContainsPickleIndex(displayName, t.PickleIndex.Value))
               ?? (candidates.Count == 1 ? candidates[0] : null);
    }

    private static bool MatchTestByNameAndPickle(TestInfo test, string strippedDisplayName, int? extractedPickleIndex)
    {

        var baseId = test.ParentId ?? test.Id;
        var methodName = baseId.Contains('.') ? baseId[(baseId.LastIndexOf('.') + 1)..] : baseId;

        // Specific rules for Scenario Outlines
        if (extractedPickleIndex.HasValue)
        {
            if (test.PickleIndex != extractedPickleIndex) return false;

            if (methodName.StartsWith(strippedDisplayName, StringComparison.Ordinal)) return true;
            if (!string.IsNullOrEmpty(test.Label) && test.Label.StartsWith(strippedDisplayName, StringComparison.Ordinal)) return true;

            return false;
        }
        
        // "Normal" scenarios
        if (string.Equals(methodName, strippedDisplayName, StringComparison.Ordinal))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(test.Label) && string.Equals(test.Label, strippedDisplayName, StringComparison.Ordinal))
        {
            return true;
        }


        return false;
    }

    private static int? ExtractPickleIndex(string testDisplayName)
    {
        if (string.IsNullOrWhiteSpace(testDisplayName))
            return null;

        // Pattern matches:
        // - __pickleIndex: "0" (xUnit2/xUnit3 with colon)
        // - __pickleIndex:"0" (without space)
        // - 0 as the 4th parameter (MSTest positional)
        // - "0" as the 4th parameter (NUnit positional)

        // Try xUnit format first (most specific)
        var xunitMatch = Regex.Match(testDisplayName, @"__pickleIndex\s*:\s*""(\d+)""");
        if (xunitMatch.Success && int.TryParse(xunitMatch.Groups[1].Value, out var pickleIndex))
            return pickleIndex;

        // Try MSTest/NUnit format (last parameter for MSTest, second to last for NUnit)
        var positionalMatch = Regex.Match(testDisplayName, @"\(([^,]+),([^,]+),([^,]+),""?(\d+)""?");
        if (positionalMatch.Success && int.TryParse(positionalMatch.Groups[^1].Value, out pickleIndex))
            return pickleIndex;

        return null;
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

    private static string? TrimOrNull(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length > 0 ? trimmed : null;
    }
}
