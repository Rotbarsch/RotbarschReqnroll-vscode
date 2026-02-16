using System.Diagnostics;
using System.Threading;
using Reqnroll.LanguageServer.Models.DotnetBuild;

namespace Reqnroll.LanguageServer.Services;

public class DotnetBuildService
{
    private readonly VsCodeOutputLogger _logger;
    private readonly Dictionary<string, Process> _activeBuilds = new();
    private readonly SemaphoreSlim _buildLock = new(1, 1);

    public DotnetBuildService(VsCodeOutputLogger logger)
    {
        _logger = logger;
    }

    public async Task<BuildResult> BuildCsProject(string projectFile, bool forceBuild=false, CancellationToken cancellationToken=default)
    {
        if (string.IsNullOrWhiteSpace(projectFile))
        {
            var message = "Project file path is empty.";
            _logger.LogWarning(message);
            return new BuildResult
            {
                Success = false,
                Message = message
            };
        }

        await _buildLock.WaitAsync(cancellationToken);
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{projectFile}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(projectFile)
            };

            var process = new Process { StartInfo = processStartInfo };

            process.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    _logger.LogInfo($"[dotnet build] {args.Data}");
                }
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    _logger.LogWarning($"[dotnet build] {args.Data}");
                }
            };

            process.Exited += (sender, args) =>
            {
                _logger.LogInfo($"[dotnet build] Process exited");
                _activeBuilds.Remove(projectFile);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            _activeBuilds[projectFile] = process;

            var message = $"Started dotnet build for project: {projectFile}";
            _logger.LogInfo(message);

            await process.WaitForExitAsync(cancellationToken);

            return new BuildResult
            {
                Success = process.ExitCode == 0,
                Message = message,
                ProjectFile = projectFile
            };
        }
        catch (Exception ex)
        {
            var message = $"Failed to execute dotnet build: {ex.Message}";
            _logger.LogError(message);
            return new BuildResult
            {
                Success = false,
                Message = message,
                ProjectFile = projectFile
            };
        }
        finally
        {
            _buildLock.Release();
        }
    }
}
