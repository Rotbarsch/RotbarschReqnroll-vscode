using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Reqnroll.LanguageServer.Models.FeatureCsParser;
using Reqnroll.LanguageServer.Services.GeneratedCsParser;

namespace Reqnroll.LanguageServer.Services;

public class FeatureCsParserService(VsCodeOutputLogger logger)
{
    public GeneratedCsHierarchy GetHierarchy(string generatedCsPath)
    {
        var fileContent = File.ReadAllText(generatedCsPath);
        var tree = CSharpSyntaxTree.ParseText(fileContent);
        var root = tree.GetRoot();

        var namespaceNode = root.DescendantNodes()
            .OfType<NamespaceDeclarationSyntax>()
            .FirstOrDefault();

        var namespaceName = namespaceNode?.Name.ToString() ?? string.Empty;
        var result = new GeneratedCsHierarchy()
        {
            Namespace = namespaceName
        };

        var classes = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>();

        var featureNodes = new List<FeatureNode>();
        foreach (var classNode in classes)
        {
            var className = classNode.Identifier.Text;

            // Identify used test framework for later usage
            var testFrameworkParser = GetTestFrameworkSpecificParser(classNode);
            if (testFrameworkParser is null)
            {
                logger.LogWarning($"Class '{className}' does not seem to be a class supporting any of the currently supported test frameworks.");
                continue;
            }

            var featureName = testFrameworkParser.GetFeatureName(classNode);
            if (featureName is null) continue;

            var scenarioNodes = new List<ScenarioNode>();

            foreach (var method in classNode.Members.OfType<MethodDeclarationSyntax>())
            {
                var methodName = method.Identifier.Text;
                var scenarioName = testFrameworkParser.GetScenarioName(method);
                if (scenarioName is null) continue;

                scenarioNodes.Add(new ScenarioNode() { MethodName = methodName, ScenarioName = scenarioName });
            }

            var featureNode = new FeatureNode { ClassName = className, FeatureName = featureName, ScenarioNodes = scenarioNodes };
            featureNodes.Add(featureNode);
        }

        result.FeatureNodes = featureNodes;
        return result;
    }

    private static IFrameworkSpecificFeatureCsParser? GetTestFrameworkSpecificParser(ClassDeclarationSyntax classNode)
    {
        foreach (var attribute in classNode.AttributeLists.SelectMany(a => a.Attributes))
        {
            var fullName = attribute.Name.ToString();

            // Reqnroll generates different class attributes per framework
            // Detect via known framework markers

            if (fullName.Contains("NUnit"))
                return new NUnitFeatureCsParser();

            if (fullName.Contains("Microsoft.VisualStudio.TestTools"))
                return new MsTestFeatureCsParser();
        }

        // XUnit does not have a specific class attribute, so we need to detect via method attributes
        var methodAttributes = classNode.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .SelectMany(m => m.AttributeLists.SelectMany(a => a.Attributes))
            .Select(a => a.Name.ToString());

        if (methodAttributes.Any(attr => attr.Contains("FactAttribute", StringComparison.OrdinalIgnoreCase) ||
                                           attr.Contains("TheoryAttribute", StringComparison.OrdinalIgnoreCase)))
        {
            return new XUnitFeatureCsParser();
        }

        return null;
    }
}