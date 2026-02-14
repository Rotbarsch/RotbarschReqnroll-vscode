using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Reqnroll.LanguageServer.Helpers;

public static class FeatureCsParserHelper
{
    public static GeneratedCsHierarchy GetHierarchy(string generatedCsPath)
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
            var featureName = GetFeatureName(classNode);
            if (featureName is null) continue;

            
            var scenarioNodes = new List<ScenarioNode>();
            foreach (var method in classNode.Members.OfType<MethodDeclarationSyntax>())
            {
                var methodName = method.Identifier.Text;
                var scenarioName = GetScenarioName(method);
                if (scenarioName is null) continue;
                scenarioNodes.Add(new ScenarioNode(){MethodName = methodName,ScenarioName = scenarioName});
            }
            var featureNode = new FeatureNode { ClassName = className, FeatureName = featureName,ScenarioNodes = scenarioNodes};
            featureNodes.Add(featureNode);
        }

        result.FeatureNodes = featureNodes;
        return result;
    }

    private static string? GetScenarioName(MethodDeclarationSyntax method)
    {
        string? scenarioName = null;

        var scenarioDescriptionAttribute = method.AttributeLists
            .SelectMany(a => a.Attributes)
            .FirstOrDefault(attr => attr.Name.ToString().Contains("Description", StringComparison.OrdinalIgnoreCase));

        if (scenarioDescriptionAttribute?.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax scenarioLiteral &&
            scenarioLiteral.IsKind(SyntaxKind.StringLiteralExpression))
        {
            scenarioName = scenarioLiteral.Token.ValueText;
        }

        return scenarioName;
    }

    private static string? GetFeatureName(ClassDeclarationSyntax classNode)
    {
        string? featureName = null;

        var featureDescriptionAttribute = classNode.AttributeLists
            .SelectMany(a => a.Attributes)
            .FirstOrDefault(attr => attr.Name.ToString().Contains("Description", StringComparison.OrdinalIgnoreCase));

        if (featureDescriptionAttribute?.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax featureLiteral &&
            featureLiteral.IsKind(SyntaxKind.StringLiteralExpression))
        {
            featureName = featureLiteral.Token.ValueText;
        }

        return featureName;
    }
}

public record GeneratedCsHierarchy
{
    public IEnumerable<FeatureNode> FeatureNodes { get; set; } = new List<FeatureNode>();
    public required string Namespace { get; set; }
}

public record FeatureNode
{
    public required string FeatureName { get; set; }
    public required string ClassName { get; set; }

    public IEnumerable<ScenarioNode> ScenarioNodes { get; set; } = new List<ScenarioNode>();
}

public record ScenarioNode
{
    public required string ScenarioName { get; set; }
    public required string MethodName { get; set; }
}