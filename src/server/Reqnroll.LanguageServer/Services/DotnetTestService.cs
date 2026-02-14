using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Reqnroll.LanguageServer.Helpers;
using Reqnroll.LanguageServer.Models.TestRunner;

namespace Reqnroll.LanguageServer.Services;

public class DotnetTestService
{
    private readonly VsCodeOutputLogger _logger;

    public DotnetTestService(VsCodeOutputLogger logger)
    {
        _logger = logger;
    }

    public async Task<IEnumerable<TestResult>> RunTestAsync(string csProjFilePath, string testQualifiedName)
    {
        var trxDir = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "rotbarsch.reqnroll", "test_results", testQualifiedName);
        if (Directory.Exists(trxDir)) Directory.Delete(trxDir, true);
        Directory.CreateDirectory(trxDir);
        var trxFilePath = Path.Join(trxDir, "test_results.trx");

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"test \"{csProjFilePath}\" --no-restore " +
                        $"--filter FullyQualifiedName=\"{testQualifiedName}\" " +
                        $"-l:trx;LogFileName=\"{trxFilePath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(csProjFilePath)
        };

        var process = new Process { StartInfo = processStartInfo };

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

        var output = outputBuilder.ToString();
        var error = errorBuilder.ToString();
        var combinedOutput = string.IsNullOrWhiteSpace(error) ? output : $"{output}\n{error}";

        var testResults = TrxResultParserHelper.Parse(trxFilePath)?.ToList();

        if (testResults is null)
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

        return result;
    }
}