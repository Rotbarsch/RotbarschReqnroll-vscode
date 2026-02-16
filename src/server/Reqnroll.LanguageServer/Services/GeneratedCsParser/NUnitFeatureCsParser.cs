using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Reqnroll.LanguageServer.Services.GeneratedCsParser;

public class NUnitFeatureCsParser : IFrameworkSpecificFeatureCsParser
{
    public string? GetFeatureName(ClassDeclarationSyntax classNode)
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