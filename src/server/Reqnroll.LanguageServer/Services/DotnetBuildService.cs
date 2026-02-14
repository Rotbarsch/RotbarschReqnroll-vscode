using System;
using System.Diagnostics;
using Reqnroll.LanguageServer.Helpers;
using Reqnroll.LanguageServer.Models.DotnetWatch;

namespace Reqnroll.LanguageServer.Services;

public class DotnetBuildService
{
    private readonly VsCodeOutputLogger _logger;
    private readonly DocumentStorageService _documentStorageService;
    private readonly Dictionary<string, Process> _activeBuilds = new();
    private readonly Dictionary<string, DateTimeOffset> _recentBuilds = new();

    public DotnetBuildService(
        VsCodeOutputLogger logger,
        DocumentStorageService documentStorageService)
    {
        _logger = logger;
        _documentStorageService = documentStorageService;
    }

    public async Task<BuildResult> HandleStartWatchRequestAsync(StartBuildParams request, CancellationToken cancellationToken)
    {
        _logger.LogInfo($"startBuild handler invoked for URI: {request.FeatureFileUri}");

        var featureFilePath = _documentStorageService.GetFullFilePath(request.FeatureFileUri);
        if (string.IsNullOrEmpty(featureFilePath))
            return new BuildResult()
            { Message = "Unable to get path of feature file.", ProjectFile = null, Success = false };
        var projectFile = ProjectFileFinder.GetProjectFileOfFeatureFile(featureFilePath);

        if (string.IsNullOrEmpty(projectFile))
        {
            var message = $"No project file found for feature file: {featureFilePath}";
            _logger.LogWarning(message);
            return new BuildResult
            {
                Success = false,
                Message = message
            };
        }

        // During start up, every feature file will be sent to this service and thus trigger a build.
        // In order to avoid multiple builds in parallel, we debounce in a 30 sec interval.
        if (_recentBuilds.TryGetValue(projectFile, out var lastBuild) &&
            DateTimeOffset.UtcNow - lastBuild < TimeSpan.FromSeconds(30))
        {
            var message = $"Skipping build for project: {projectFile}. Last build was less than 30 seconds ago.";
            _logger.LogInfo(message);
            return new BuildResult
            {
                Success = true,
                Message = message,
                ProjectFile = projectFile
            };
        }

        // Start dotnet build process
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

            // Log output and errors
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

            _recentBuilds[projectFile] = DateTimeOffset.UtcNow;

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
    }
}
