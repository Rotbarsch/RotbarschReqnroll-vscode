using Reqnroll.LanguageServer.Models.TestRunner;

namespace Reqnroll.LanguageServer.Services.TestRunning;

// Runs tests for any dotnet-test-compatible framework (xUnit, NUnit, MSTest, ...) by invoking
// the `dotnet test` CLI directly (Process.Start), instead of driving vstest.console.exe
// programmatically. This avoids having to bundle vstest.console.exe (and its ~400 dependency
// DLLs) with the extension.
public interface IDotnetTestRunner
{
    Task<IReadOnlyList<TestResult>> RunTestsAsync(
        string csProjFilePath,
        IReadOnlyList<TestInfo> tests,
        Action<TestResult>? onTestCompleted = null,
        CancellationToken cancellationToken = default);
}
