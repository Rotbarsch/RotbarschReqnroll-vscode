using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Reqnroll.LanguageServer.Models.FeatureCsParser;

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

    public bool IsScenarioOutline(MethodDeclarationSyntax method)
    {
        return method.AttributeLists.SelectMany(x => x.Attributes).Select(x => x.Name.ToString())
            .Any(x => x.Contains("TestCaseAttribute"));
    }

    public IEnumerable<ExampleRow> GetExampleRows(MethodDeclarationSyntax method)
    {
        var result = new List<ExampleRow>();
        var relevantAttributes = method.AttributeLists.SelectMany(x => x.Attributes).Where(x=>x.ToString().Contains("TestCaseAttribute"));

        foreach (var a in relevantAttributes.Where(x=>x.ArgumentList is not null))
        {
            var pickleIndex = (a.ArgumentList!.Arguments[^2].Expression as LiteralExpressionSyntax)?.Token.ValueText ?? "-1";
            var arguments = a.ArgumentList.Arguments.Take(a.ArgumentList.Arguments.Count - 2)
                .Where(x=>x.Expression is LiteralExpressionSyntax)
                .Select(x => x.Expression as LiteralExpressionSyntax)
                .Select(x => x?.Token.ValueText);

            result.Add(new ExampleRow
            {
                Arguments = string.Join(",", arguments),
                PickleIndex = int.Parse(pickleIndex)
            });
        }

        return result;
    }
}