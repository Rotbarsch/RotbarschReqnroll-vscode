using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Testing.Platform.ServerMode.Client;
using Reqnroll.LanguageServer.Helpers;
using Reqnroll.LanguageServer.Models.TestRunner;
using Reqnroll.LanguageServer.Services;

namespace Reqnroll.LanguageServer.Services.TestRunning;

// Drives test projects that opted into Microsoft.Testing.Platform (MTP) instead of VSTest (e.g.
// xunit.v3 without the VSTest bridge). `dotnet test`'s VSTest mode cannot run these projects at all
// on .NET 10+ SDKs, so instead of shelling out to `dotnet test`, this launches the already-built
// test executable directly and drives it over MTP's JSON-RPC "server mode" protocol via the
// source-only client from Microsoft.Testing.Platform.ServerMode.Client.Sources. Every run does a
// discovery pass first (to map MTP's opaque, hash-based test uids back to our TestInfo ids) and then
// a second launch to execute only the matched tests -- a single connection cannot both discover and
// run when the server doesn't advertise MultiRequestSupport.
public sealed class MtpTestRunner : IDotnetTestRunner
{
    private static readonly Regex PickleIndexRegex = new(
        @"__pickleIndex\s*:\s*""(\d+)""",
        RegexOptions.Compiled);

    private readonly VsCodeOutputLogger _logger;

    public MtpTestRunner(VsCodeOutputLogger logger)
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

        var dllPath = ProjectOutputDllFinder.GetOutputDllPath(csProjFilePath);
        if (dllPath is null || !File.Exists(dllPath))
        {
            return Fail(tests, onTestCompleted, "Could not find output DLL for the project. Make sure the project builds successfully.");
        }

        var reported = new Dictionary<string, TestResult>();

        void ReportTestResult(TestInfo test, MtpTestNodeUpdate node)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(node.ErrorMessage))
            {
                sb.AppendLine(node.ErrorMessage);
                if (!string.IsNullOrEmpty(node.ErrorStackTrace))
                {
                    sb.AppendLine(node.ErrorStackTrace);
                }
                sb.AppendLine();
            }
            if (!string.IsNullOrEmpty(node.StandardOutput))
            {
                sb.Append(node.StandardOutput);
            }

            var testResult = new TestResult
            {
                Id = test.Id,
                Passed = node.ExecutionState == "passed",
                Message = sb.ToString(),
            };
            reported[test.Id] = testResult;
            onTestCompleted?.Invoke(testResult);
        }

        IReadOnlyList<MtpTestNodeUpdate> discoveredNodes;
        try
        {
            discoveredNodes = await DiscoverAsync(dllPath, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError($"MTP test discovery failed for '{dllPath}': {ex.Message}");
            return Fail(tests, onTestCompleted, $"Discovering tests failed: {ex.Message}");
        }

        // Match every discovered MTP test node back to a requested TestInfo (same id scheme as the
        // VSTest path: FullyQualifiedName for simple tests, ParentId + pickle index for Theory rows).
        var uidToTest = new Dictionary<string, TestInfo>();
        var matchedTestIds = new HashSet<string>();
        foreach (var node in discoveredNodes)
        {
            if (node.Uid is null) continue;

            var test = MatchTestFromNode(tests, node, matchedTestIds);
            if (test is not null)
            {
                uidToTest[node.Uid] = test;
                matchedTestIds.Add(test.Id);
            }
        }

        if (uidToTest.Count > 0)
        {
            try
            {
                await RunMatchedTestsAsync(dllPath, uidToTest, ReportTestResult, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Running MTP tests failed for '{dllPath}': {ex.Message}");
            }
        }

        var unreported = tests.Where(t => !reported.ContainsKey(t.Id)).ToList();
        foreach (var test in unreported)
        {
            var failResult = new TestResult
            {
                Id = test.Id,
                Passed = false,
                Message = "The test could not be found or did not report a result when running it through Microsoft.Testing.Platform.",
            };
            reported[test.Id] = failResult;
            onTestCompleted?.Invoke(failResult);
        }

        return tests.Select(t => reported[t.Id]).ToList();
    }

    private async Task<IReadOnlyList<MtpTestNodeUpdate>> DiscoverAsync(string dllPath, CancellationToken cancellationToken)
    {
        var discoveredNodes = new List<MtpTestNodeUpdate>();

        using var client = await MtpServerClient.LaunchAsync(dllPath, cancellationToken: cancellationToken);
        await client.InitializeAsync(cancellationToken);

        client.LogReceived += (_, log) => _logger.LogInfo($"[mtp discover] {log.Message}");
        client.TestNodesUpdated += (_, update) =>
        {
            foreach (var change in update.Changes)
            {
                if (change.NodeType == "action")
                {
                    discoveredNodes.Add(change);
                }
            }
        };

        await client.DiscoverTestsAsync(cancellationToken);
        await client.ExitAsync(cancellationToken);
        await client.ShutdownAsync();

        return discoveredNodes;
    }

    private async Task RunMatchedTestsAsync(
        string dllPath,
        IReadOnlyDictionary<string, TestInfo> uidToTest,
        Action<TestInfo, MtpTestNodeUpdate> reportTestResult,
        CancellationToken cancellationToken)
    {
        using var client = await MtpServerClient.LaunchAsync(dllPath, cancellationToken: cancellationToken);
        await client.InitializeAsync(cancellationToken);

        client.LogReceived += (_, log) => _logger.LogInfo($"[mtp run] {log.Message}");
        client.TestNodesUpdated += (_, update) =>
        {
            foreach (var change in update.Changes)
            {
                if (change.ExecutionState is null or "discovered" or "in-progress") continue;
                if (change.Uid is not null && uidToTest.TryGetValue(change.Uid, out var test))
                {
                    reportTestResult(test, change);
                }
            }
        };

        try
        {
            await client.RunTestsAsync(uidToTest.Keys.ToHashSet(), cancellationToken);
        }
        finally
        {
            await client.ExitAsync(CancellationToken.None);
            await client.ShutdownAsync();
        }
    }

    private static TestInfo? MatchTestFromNode(IReadOnlyList<TestInfo> tests, MtpTestNodeUpdate node, HashSet<string> alreadyMatchedTestIds)
    {
        var fqn = GetFullyQualifiedName(node);
        if (fqn is null) return null;

        var pickleIndex = ExtractPickleIndex(node.DisplayName ?? string.Empty);

        var direct = tests.FirstOrDefault(t =>
            !alreadyMatchedTestIds.Contains(t.Id) &&
            t.PickleIndex is null &&
            string.Equals(t.Id, fqn, StringComparison.Ordinal));
        if (direct is not null) return direct;

        if (pickleIndex.HasValue)
        {
            var paramMatch = tests.FirstOrDefault(t =>
                !alreadyMatchedTestIds.Contains(t.Id) &&
                t.PickleIndex == pickleIndex &&
                string.Equals(t.ParentId, fqn, StringComparison.Ordinal));
            if (paramMatch is not null) return paramMatch;
        }

        return null;
    }

    private static string? GetFullyQualifiedName(MtpTestNodeUpdate node)
    {
        if (!node.Node.TryGetValue("location.type", out var typeObj) || typeObj is not string type)
            return null;
        if (!node.Node.TryGetValue("location.method", out var methodObj) || methodObj is not string method)
            return null;

        var parenIndex = method.IndexOf('(');
        var bareMethod = parenIndex >= 0 ? method[..parenIndex] : method;
        return $"{type}.{bareMethod}";
    }

    private static int? ExtractPickleIndex(string displayName)
    {
        var match = PickleIndexRegex.Match(displayName);
        return match.Success && int.TryParse(match.Groups[1].Value, out var index) ? index : null;
    }

    private static List<TestResult> Fail(IReadOnlyList<TestInfo> tests, Action<TestResult>? onTestCompleted, string message)
    {
        var results = tests.Select(t => new TestResult
        {
            Id = t.Id,
            Passed = false,
            Message = message,
        }).ToList();

        foreach (var r in results) onTestCompleted?.Invoke(r);

        return results;
    }
}
