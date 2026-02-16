using Reqnroll.LanguageServer.Helpers;
using Reqnroll.LanguageServer.Models.DotnetBuild;
using Reqnroll.LanguageServer.Services;

namespace Reqnroll.LanguageServer.Handlers;

public class DotnetBuildRequestHandler
{
    private readonly VsCodeOutputLogger _logger;
    private readonly DocumentStorageService _documentStorageService;
    private readonly DotnetBuildService _dotnetBuildService;

    public DotnetBuildRequestHandler(
        VsCodeOutputLogger logger,
        DocumentStorageService documentStorageService,
        DotnetBuildService dotnetBuildService)
    {
        _logger = logger;
        _documentStorageService = documentStorageService;
        _dotnetBuildService = dotnetBuildService;
    }

    public Task<BuildResult> HandleStartBuildRequestAsync(StartBuildParams request, CancellationToken cancellationToken)
    {
        _logger.LogInfo($"startBuild handler invoked for URI: {request.FeatureFileUri}");

        var featureFilePath = _documentStorageService.GetFullFilePath(request.FeatureFileUri);
        if (string.IsNullOrEmpty(featureFilePath))
        {
            return Task.FromResult(new BuildResult
            {
                Message = "Unable to get path of feature file.",
                ProjectFile = null,
                Success = false
            });
        }

        var projectFile = ProjectFileFinder.GetProjectFileOfFeatureFile(featureFilePath);

        if (string.IsNullOrEmpty(projectFile))
        {
            var message = $"No project file found for feature file: {featureFilePath}";
            _logger.LogWarning(message);
            return Task.FromResult(new BuildResult
            {
                Success = false,
                Message = message
            });
        }

        return _dotnetBuildService.BuildCsProject(projectFile, false, cancellationToken);
    }

    public Task<BuildResult> HandleForceBuildRequestAsync(StartBuildParams request, CancellationToken cancellationToken)
    {
        _logger.LogInfo($"forceBuild handler invoked for URI: {request.FeatureFileUri}");

        var featureFilePath = _documentStorageService.GetFullFilePath(request.FeatureFileUri);
        if (string.IsNullOrEmpty(featureFilePath))
        {
            return Task.FromResult(new BuildResult
            {
                Message = "Unable to get path of feature file.",
                ProjectFile = null,
                Success = false
            });
        }

        var projectFile = ProjectFileFinder.GetProjectFileOfFeatureFile(featureFilePath);

        if (string.IsNullOrEmpty(projectFile))
        {
            var message = $"No project file found for feature file: {featureFilePath}";
            _logger.LogWarning(message);
            return Task.FromResult(new BuildResult
            {
                Success = false,
                Message = message
            });
        }

        // Force build: build with restore
        return _dotnetBuildService.BuildCsProject(projectFile, true, cancellationToken);
    }
}