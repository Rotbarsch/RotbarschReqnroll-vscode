using Reqnroll.LanguageServer.Helpers;
using Reqnroll.LanguageServer.Models.TestRunner;
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

    private string GetDotNetTestFilterString(string testQualifiedName, int? pickleIndex=null)
    {
        if (pickleIndex.HasValue)
        {
            // This covers MSTest, NUnit and xUnit
            return $"FullyQualifiedName={testQualifiedName}&(Name~{pickleIndex}|DisplayName~{pickleIndex})";
        }
        return $"FullyQualifiedName={testQualifiedName}";
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