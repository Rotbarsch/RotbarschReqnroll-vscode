using Reqnroll.LanguageServer.Helpers;
using Reqnroll.LanguageServer.Models.TestRunner;
using Reqnroll.LanguageServer.Models.TrxResultParserHelper;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Reqnroll.LanguageServer.Services;

public class DotnetTestService
{
    private readonly VsCodeOutputLogger _logger;
    private SemaphoreSlim _testLock;
    private readonly List<Process> _runningProcesses = new();
    private readonly object _processListLock = new();
    private int _parallelExecutionLimit = 5;

    public DotnetTestService(VsCodeOutputLogger logger)
    {
        _logger = logger;
        _testLock = new SemaphoreSlim(_parallelExecutionLimit, _parallelExecutionLimit);
    }

    public void SetParallelExecutionLimit(int limit)
    {
        if (limit < 1 || limit > 20)
        {
            _logger.LogWarning($"Invalid parallel execution limit {limit}. Must be between 1 and 20. Using previous value {_parallelExecutionLimit}.");
            return;
        }

        if (_parallelExecutionLimit != limit)
        {
            _logger.LogInfo($"Changing parallel execution limit from {_parallelExecutionLimit} to {limit}");
            _parallelExecutionLimit = limit;
            
            // Dispose old semaphore and create new one with updated limit
            var oldLock = _testLock;
            _testLock = new SemaphoreSlim(limit, limit);
            oldLock.Dispose();
        }
    }

    public void KillAllRunningProcesses()
    {
        lock (_processListLock)
        {
            _logger.LogInfo($"Killing {_runningProcesses.Count} running test processes...");
            foreach (var process in _runningProcesses.ToList())
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(true); // true = kill entire process tree
                        _logger.LogInfo($"Killed process {process.Id}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to kill process: {ex.Message}");
                }
            }
            _runningProcesses.Clear();
        }
    }

    public async Task<IEnumerable<TestResult>> RunTestAsync(string csProjFilePath, string testQualifiedName, int? pickleIndex=null)
    {
        await _testLock.WaitAsync();
        try
        {
            return await RunTestInternalAsync(csProjFilePath, testQualifiedName, pickleIndex);
        }
        finally
        {
            _testLock.Release();
        }
    }

    private async Task<IEnumerable<TestResult>> RunTestInternalAsync(string csProjFilePath, string testQualifiedName, int? pickleIndex=null)
    {
        var trxDir = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "rotbarsch.reqnroll", "test_results", SanitizeFolderName(testQualifiedName,pickleIndex));
        if (Directory.Exists(trxDir)) Directory.Delete(trxDir, true);
        Directory.CreateDirectory(trxDir);
        var trxFilePath = Path.Join(trxDir, "test_results.trx");

        var dllPath = ProjectOutputDllFinder.GetOutputDllPath(csProjFilePath);
        if (dllPath == null)
        {
            return new List<TestResult>()
            {
                new TestResult
                {
                    Id = testQualifiedName,
                    Message = "Could not find output DLL for the project. Make sure the project builds successfully.",
                    Line = 0,
                    Passed = false
                }
            };
        }

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(csProjFilePath)
        };
        processStartInfo.ArgumentList.Add("test");

        processStartInfo.ArgumentList.Add(csProjFilePath);
        processStartInfo.ArgumentList.Add("--no-restore");
        processStartInfo.ArgumentList.Add("--no-build");
        processStartInfo.ArgumentList.Add("--filter");
        processStartInfo.ArgumentList.Add(GetDotNetTestFilterString(testQualifiedName, pickleIndex));
        processStartInfo.ArgumentList.Add("-l:trx;LogFileName=" + trxFilePath);

        _logger.LogInfo("Running dotnet " + string.Join(" ", processStartInfo.ArgumentList));

        var process = new Process { StartInfo = processStartInfo };

        // Track the running process
        lock (_processListLock)
        {
            _runningProcesses.Add(process);
        }

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        // Capture output and errors
        process.OutputDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrEmpty(args.Data))
            {
                outputBuilder.AppendLine(args.Data);
                _logger.LogInfo($"[dotnet test] {args.Data}");
            }
        };

        process.ErrorDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrEmpty(args.Data))
            {
                errorBuilder.AppendLine(args.Data);
                _logger.LogWarning($"[dotnet test] {args.Data}");
            }
        };

        process.Exited += (sender, args) =>
        {
            _logger.LogInfo($"[dotnet test] Process exited");
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        // Remove from tracking list when process completes
        lock (_processListLock)
        {
            _runningProcesses.Remove(process);
        }

        var output = outputBuilder.ToString();
        var error = errorBuilder.ToString();
        var combinedOutput = string.IsNullOrWhiteSpace(error) ? output : $"{output}\n{error}";

        var testResults = TrxResultParserHelper.Parse(trxFilePath)?.ToList();

        if (testResults is null || (testResults.Count == 0 && process.ExitCode != 0))
        {
            _logger.LogError($"[dotnet test] Process failed with exit code {process.ExitCode}");
            
            return [new TestResult
            {
                Id = testQualifiedName,
                Message = string.IsNullOrWhiteSpace(combinedOutput)
                    ? $"Test failed with exit code {process.ExitCode}"
                    : combinedOutput,
                Line = 0,
                Passed = false
            }];
        }

        var result = new List<TestResult>();

        if (testResults.Count == 1)
        {
            result.Add(new TestResult
            {
                Id = testQualifiedName,
                Message = testResults.Single().StdOut,
                Line = 0,
                Passed = testResults.Single().Outcome == "Passed"
            });

        }
        else
        {
            // Now add results for the test methods
            foreach (var r in testResults)
            {
                result.Add(new TestResult
                {
                    Id = r.FullMethodName,
                    Message = r.StdOut,
                    Line = 0,
                    Passed = r.Outcome == "Passed"
                });
            }
        }

        return result;
    }

    public async Task<IEnumerable<TestResult>> RunTestsBatchAsync(string csProjFilePath, List<TestInfo> tests)
    {
        await _testLock.WaitAsync();
        try
        {
            return await RunTestsBatchInternalAsync(csProjFilePath, tests);
        }
        finally
        {
            _testLock.Release();
        }
    }

    private async Task<IEnumerable<TestResult>> RunTestsBatchInternalAsync(string csProjFilePath, List<TestInfo> tests)
    {
        var batchId = $"batch_{Guid.NewGuid():N}";
        var trxDir = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "rotbarsch.reqnroll", "test_results", SanitizeFolderName(batchId, null));
        if (Directory.Exists(trxDir)) Directory.Delete(trxDir, true);
        Directory.CreateDirectory(trxDir);
        var trxFilePath = Path.Join(trxDir, "test_results.trx");

        var dllPath = ProjectOutputDllFinder.GetOutputDllPath(csProjFilePath);
        if (dllPath == null)
        {
            return tests.Select(t => new TestResult
            {
                Id = t.Id,
                Message = "Could not find output DLL for the project. Make sure the project builds successfully.",
                Line = 0,
                Passed = false
            });
        }

        var combinedFilter = BuildCombinedFilter(tests);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(csProjFilePath)
        };
        processStartInfo.ArgumentList.Add("test");
        processStartInfo.ArgumentList.Add(csProjFilePath);
        processStartInfo.ArgumentList.Add("--no-restore");
        processStartInfo.ArgumentList.Add("--no-build");
        processStartInfo.ArgumentList.Add("--filter");
        processStartInfo.ArgumentList.Add(combinedFilter);
        processStartInfo.ArgumentList.Add("-l:trx;LogFileName=" + trxFilePath);

        _logger.LogInfo("Running dotnet " + string.Join(" ", processStartInfo.ArgumentList));

        var process = new Process { StartInfo = processStartInfo };

        lock (_processListLock)
        {
            _runningProcesses.Add(process);
        }

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrEmpty(args.Data))
            {
                outputBuilder.AppendLine(args.Data);
                _logger.LogInfo($"[dotnet test] {args.Data}");
            }
        };

        process.ErrorDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrEmpty(args.Data))
            {
                errorBuilder.AppendLine(args.Data);
                _logger.LogWarning($"[dotnet test] {args.Data}");
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        lock (_processListLock)
        {
            _runningProcesses.Remove(process);
        }

        var output = outputBuilder.ToString();
        var error = errorBuilder.ToString();
        var combinedOutput = string.IsNullOrWhiteSpace(error) ? output : $"{output}\n{error}";

        var testResults = TrxResultParserHelper.Parse(trxFilePath)?.ToList();

        if (testResults is null || (testResults.Count == 0 && process.ExitCode != 0))
        {
            _logger.LogError($"[dotnet test] Process failed with exit code {process.ExitCode}");
            return tests.Select(t => new TestResult
            {
                Id = t.Id,
                Message = string.IsNullOrWhiteSpace(combinedOutput)
                    ? $"Test failed with exit code {process.ExitCode}"
                    : combinedOutput,
                Line = 0,
                Passed = false
            });
        }

        return MapTrxResults(testResults, tests);
    }

    private string BuildCombinedFilter(List<TestInfo> tests)
    {
        var filterParts = tests.Select(GetFilterForTest).Distinct().ToList();
        return string.Join("|", filterParts);
    }

    private string GetFilterForTest(TestInfo test)
    {
        if (test.PickleIndex.HasValue)
        {
            var parentId = test.ParentId ?? test.Id;
            return $"(FullyQualifiedName~{parentId}&(Name~{test.PickleIndex}|DisplayName~{test.PickleIndex}))";
        }
        return $"FullyQualifiedName~{test.Id}";
    }

    private List<TestResult> MapTrxResults(List<TrxTestCaseResult> trxResults, List<TestInfo> requestedTests)
    {
        var result = new List<TestResult>();

        // Index requested tests by their base method name
        // For example rows: key = parentId (Ns.Class.Method)
        // For simple scenarios: key = id (Ns.Class.Method)
        var testsByMethod = new Dictionary<string, List<TestInfo>>();
        foreach (var test in requestedTests)
        {
            var key = test.ParentId ?? test.Id;
            if (!testsByMethod.TryGetValue(key, out var list))
            {
                list = new List<TestInfo>();
                testsByMethod[key] = list;
            }
            list.Add(test);
        }

        var matchedTestIds = new HashSet<string>();

        foreach (var trx in trxResults)
        {
            // Strip parameters from FullMethodName to handle NUnit/xUnit
            // which include params like: Method("val1","val2",...)
            var baseMethod = StripMethodParameters(trx.FullMethodName);

            if (!testsByMethod.TryGetValue(baseMethod, out var candidates))
            {
                result.Add(new TestResult { Id = baseMethod, Message = trx.StdOut, Line = 0, Passed = trx.Outcome == "Passed" });
                continue;
            }

            TestInfo? matched = null;

            if (candidates.Count == 1 && !candidates[0].PickleIndex.HasValue)
            {
                // Simple scenario - direct match
                matched = candidates[0];
            }
            else
            {
                // Scenario outline - match by pickle index in test name
                matched = candidates.FirstOrDefault(t =>
                    t.PickleIndex.HasValue &&
                    !matchedTestIds.Contains(t.Id) &&
                    TestNameContainsPickleIndex(trx.TestName, t.PickleIndex.Value));
            }

            if (matched != null)
            {
                matchedTestIds.Add(matched.Id);
                result.Add(new TestResult
                {
                    Id = matched.Id,
                    Message = trx.StdOut,
                    Line = 0,
                    Passed = trx.Outcome == "Passed"
                });
            }
            else
            {
                result.Add(new TestResult { Id = baseMethod, Message = trx.StdOut, Line = 0, Passed = trx.Outcome == "Passed" });
            }
        }

        return result;
    }

    private static string StripMethodParameters(string fullMethodName)
    {
        var parenIndex = fullMethodName.IndexOf('(');
        return parenIndex >= 0 ? fullMethodName[..parenIndex] : fullMethodName;
    }

    private static bool TestNameContainsPickleIndex(string testName, int pickleIndex)
    {
        var idx = pickleIndex.ToString();

        // xUnit: named parameter __pickleIndex: "N"
        if (testName.Contains($"__pickleIndex: \"{idx}\""))
            return true;

        // NUnit: positional params, pickle is second-to-last before null/array
        // e.g. FunWithBool("true","true","true","0",null)
        if (Regex.IsMatch(testName, $"\"" + Regex.Escape(idx) + $"\"" + @"\s*,\s*(null|\[.*?\])\s*\)\s*$"))
            return true;

        // MSTest: pickle index is last value in display name
        // e.g. "Fun with bool(true,true,true,0)"
        if (Regex.IsMatch(testName, @",\s*" + Regex.Escape(idx) + @"\s*\)\s*$"))
            return true;

        return false;
    }

    private string GetDotNetTestFilterString(string testQualifiedName, int? pickleIndex=null)
    {
        if (pickleIndex.HasValue)
        {
            // This covers MSTest, NUnit and xUnit
            return $"FullyQualifiedName~{testQualifiedName}&(Name~{pickleIndex}|DisplayName~{pickleIndex})";
        }
        return $"FullyQualifiedName~{testQualifiedName}";
    }

    private ReadOnlySpan<char> SanitizeFolderName(string input, int? pickleIndex)
    {
        var folderName = $"{input}";
        if (pickleIndex.HasValue) folderName += $"_{pickleIndex}";

        string regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        var r = new Regex($"[{Regex.Escape(regexSearch)}]");
        return r.Replace(folderName, "");
    }
}