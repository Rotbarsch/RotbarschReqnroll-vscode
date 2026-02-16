using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Reqnroll.LanguageServer.Services.GeneratedCsParser;

public class MsTestFeatureCsParser : IFrameworkSpecificFeatureCsParser
{
    public string? GetFeatureName(ClassDeclarationSyntax classNode)
    {
        string? featureName = null;

        foreach (var method in classNode.Members.OfType<MethodDeclarationSyntax>())
        {
            var attributes = method.AttributeLists.SelectMany(a => a.Attributes).ToList();

            if (!attributes.Any(attr => attr.Name.ToString().Contains("Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute")))
                continue;

            var testPropertyAttribute = attributes.FirstOrDefault(attr => attr.Name.ToString().Contains("Microsoft.VisualStudio.TestTools.UnitTesting.TestPropertyAttribute"));

            if (testPropertyAttribute?.ArgumentList?.Arguments is { Count: >= 2 } args &&
                args[0].Expression is LiteralExpressionSyntax nameLiteral && nameLiteral.IsKind(SyntaxKind.StringLiteralExpression) &&
                nameLiteral.Token.ValueText == "FeatureTitle" &&
                args[1].Expression is LiteralExpressionSyntax valueLiteral && valueLiteral.IsKind(SyntaxKind.StringLiteralExpression))
            {
                featureName = valueLiteral.Token.ValueText;
                break;
            }
        }

        return featureName;
    }

    public string? GetScenarioName(MethodDeclarationSyntax methodNode)
    {
        string? scenarioName = null;

        var scenarioDescriptionAttribute = methodNode.AttributeLists
            .SelectMany(a => a.Attributes)
            .FirstOrDefault(attr => attr.Name.ToString().Contains("Description", StringComparison.OrdinalIgnoreCase));

        if (scenarioDescriptionAttribute?.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax scenarioLiteral &&
            scenarioLiteral.IsKind(SyntaxKind.StringLiteralExpression))
        {
            scenarioName = scenarioLiteral.Token.ValueText;
        }

        return scenarioName;
    }
}