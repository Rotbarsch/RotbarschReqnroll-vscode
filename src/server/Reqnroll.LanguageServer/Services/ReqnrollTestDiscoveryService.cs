using Reqnroll.LanguageServer.Helpers;
using Reqnroll.LanguageServer.Models.TestDiscovery;

namespace Reqnroll.LanguageServer.Services;

public class ReqnrollTestDiscoveryService
{
    private readonly VsCodeOutputLogger _logger;
    private readonly DocumentStorageService _documentStorageService;

    public ReqnrollTestDiscoveryService(
        VsCodeOutputLogger logger,
        DocumentStorageService documentStorageService)
    {
        _logger = logger;
        _documentStorageService = documentStorageService;
    }

    public async Task<List<DiscoveredTest>> HandleDiscoverTestsRequestAsync(DiscoverTestsParams request, CancellationToken cancellationToken)
    {
        _logger.LogInfo($"discoverTests handler invoked for URI: {request.Uri}");

        var featurePath = _documentStorageService.GetFullFilePath(request.Uri);
        var generatedCsPath = Path.ChangeExtension(featurePath, ".feature.cs");
        if (!File.Exists(generatedCsPath))
        {
            _logger.LogWarning($"Generated CS file does not exist: {generatedCsPath}");
            return new List<DiscoveredTest>();
        }

        var hierarchy = FeatureCsParserHelper.GetHierarchy(generatedCsPath);

        var result = new List<DiscoveredTest>();
        foreach (var h in hierarchy.FeatureNodes)
        {
            result.Add(new DiscoveredTest
            {
                Id = $"{hierarchy.Namespace}.{h.ClassName}",
                Uri = request.Uri,
                Label = h.FeatureName,
                Range = new TestRange { EndCharacter = 0, EndLine = 0, StartCharacter = 0, StartLine = 0 },
                Children = h.ScenarioNodes.Select(s => new DiscoveredTest
                {
                    Id = $"{hierarchy.Namespace}.{h.ClassName}.{s.MethodName}",
                    Label = s.ScenarioName,
                    Range = new TestRange { EndCharacter = 0, EndLine = 0, StartCharacter = 0, StartLine = 0 },
                    Uri = request.Uri,

                }).ToList(),
            });
        }

        _logger.LogInfo($"Discovered {result.Count} features and {result.Where(x => x.Children is not null).SelectMany(x => x.Children!).Count()} scenarios.");

        var root = new DiscoveredTest
        {
            Uri = request.Uri,
            Id = hierarchy.Namespace,
            Label = hierarchy.Namespace,
            Range = new TestRange { EndCharacter = 0, EndLine = 0, StartCharacter = 0, StartLine = 0 },
            Children = result,
        };
        return [root];
    }
}