using System.Text.RegularExpressions;
using Reqnroll.LanguageServer.Models.TestDiscovery;

namespace Reqnroll.LanguageServer.Services;

public class ReqnrollTestDiscoveryService
{
    private readonly VsCodeOutputLogger _logger;
    private readonly DocumentStorageService _documentStorageService;
    private readonly FeatureCsParserService _csParserService;

    public ReqnrollTestDiscoveryService(
        VsCodeOutputLogger logger,
        DocumentStorageService documentStorageService,
        FeatureCsParserService csParserService)
    {
        _logger = logger;
        _documentStorageService = documentStorageService;
        _csParserService = csParserService;
    }

    public async Task<List<DiscoveredTest>> HandleDiscoverTestsRequestAsync(DiscoverTestsParams request, CancellationToken cancellationToken)
    {
        _logger.LogInfo($"discoverTests handler invoked for URI: {request.Uri}");

        var featurePath = _documentStorageService.GetFullFilePath(request.Uri);
        
        if (string.IsNullOrEmpty(featurePath))
        {
            _logger.LogWarning($"Could not resolve file path for URI: {request.Uri}");
            return new List<DiscoveredTest>();
        }
        
        // Handle both .feature and .feature.cs file URIs
        // If the URI is already for a .feature.cs file, use it directly
        // Otherwise, append .cs to get the generated C# file path
        var generatedCsPath = featurePath.EndsWith(".feature.cs", StringComparison.OrdinalIgnoreCase) 
            ? featurePath 
            : featurePath + ".cs";
            
        if (!File.Exists(generatedCsPath))
        {
            _logger.LogWarning($"Generated CS file does not exist: {generatedCsPath}");
            return new List<DiscoveredTest>();
        }

        // Get the actual .feature file path (remove .cs extension if present)
        var actualFeaturePath = featurePath.EndsWith(".feature.cs", StringComparison.OrdinalIgnoreCase)
            ? featurePath.Substring(0, featurePath.Length - 3)
            : featurePath;

        // Read the .feature file content for line number lookup
        string[]? featureFileLines = null;
        if (File.Exists(actualFeaturePath))
        {
            featureFileLines = await File.ReadAllLinesAsync(actualFeaturePath, cancellationToken);
        }
        else
        {
            _logger.LogWarning($"Feature file does not exist: {actualFeaturePath}");
        }

        var hierarchy = _csParserService.GetHierarchy(generatedCsPath);

        var result = new List<DiscoveredTest>();
        foreach (var h in hierarchy.FeatureNodes)
        {
            var scenarios = new List<DiscoveredTest>();

            // Add the scenarios
            var simpleScenarios = h.ScenarioNodes.Select(s => new DiscoveredTest
            {
                Id = $"{hierarchy.Namespace}.{h.ClassName}.{s.MethodName}",
                Label = s.ScenarioName,
                Range = FindScenarioRange(featureFileLines, s.ScenarioName),
                Uri = request.Uri,

            });
            scenarios.AddRange(simpleScenarios);

            result.Add(new DiscoveredTest
            {
                Id = $"{hierarchy.Namespace}.{h.ClassName}",
                Uri = request.Uri,
                Label = h.FeatureName,
                Range = FindFeatureRange(featureFileLines, h.FeatureName),
                Children = scenarios,
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

    private static TestRange FindFeatureRange(string[]? featureFileLines, string featureName)
    {
        if (featureFileLines == null)
        {
            return new TestRange { StartLine = 0, EndLine = 0, StartCharacter = 0, EndCharacter = 0 };
        }

        var pattern = $@"^\s*Feature:\s*{Regex.Escape(featureName)}\s*$";
        var regex = new Regex(pattern, RegexOptions.IgnoreCase);

        for (int i = 0; i < featureFileLines.Length; i++)
        {
            if (regex.IsMatch(featureFileLines[i]))
            {
                return new TestRange { StartLine = i, EndLine = i, StartCharacter = 0, EndCharacter = 0 };
            }
        }

        // Return default if not found
        return new TestRange { StartLine = 0, EndLine = 0, StartCharacter = 0, EndCharacter = 0 };
    }

    private static TestRange FindScenarioRange(string[]? featureFileLines, string scenarioName)
    {
        if (featureFileLines == null)
        {
            return new TestRange { StartLine = 0, EndLine = 0, StartCharacter = 0, EndCharacter = 0 };
        }

        var pattern = $@"^\s*Scenario(\s+Outline)?:\s*{Regex.Escape(scenarioName)}\s*$";
        var regex = new Regex(pattern, RegexOptions.IgnoreCase);

        for (int i = 0; i < featureFileLines.Length; i++)
        {
            if (regex.IsMatch(featureFileLines[i]))
            {
                return new TestRange { StartLine = i, EndLine = i, StartCharacter = 0, EndCharacter = 0 };
            }
        }

        // Return default if not found
        return new TestRange { StartLine = 0, EndLine = 0, StartCharacter = 0, EndCharacter = 0 };
    }
}