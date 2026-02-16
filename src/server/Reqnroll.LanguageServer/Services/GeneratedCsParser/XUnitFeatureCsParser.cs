using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Reqnroll.LanguageServer.Models.FeatureCsParser;

namespace Reqnroll.LanguageServer.Services.GeneratedCsParser;

public class XUnitFeatureCsParser : IFrameworkSpecificFeatureCsParser
{
    public string? GetFeatureName(ClassDeclarationSyntax classNode)
    {
        // There should should be at least one method with either an attribute whose name contains either "FactAttribute" or "TheoryAttribute".
        // That same method should have a TraitAttribute, with the first argument "FeatureTitle".
        // Return the second argument of that attribute.
        string? featureName = null;

        foreach (var method in classNode.Members.OfType<MethodDeclarationSyntax>())
        {
            var attributes = method.AttributeLists.SelectMany(a => a.Attributes).ToList();

            if (!attributes.Any(attr => attr.Name.ToString().Contains("FactAttribute", StringComparison.OrdinalIgnoreCase) ||
                                        attr.Name.ToString().Contains("TheoryAttribute", StringComparison.OrdinalIgnoreCase)))
                continue;

            var traitAttribute = attributes.FirstOrDefault(attr => attr.Name.ToString().Contains("TraitAttribute", StringComparison.OrdinalIgnoreCase));

            if (traitAttribute?.ArgumentList?.Arguments is { Count: >= 2 } args &&
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
        // The method should have either an attribute whose name contains "FactAttribute" or "TheoryAttribute".
        // THis attribute should have a named argument "DisplayName".
        // Return the value of that argument.
        string? scenarioName = null;

        var factOrTheoryAttribute = methodNode.AttributeLists
            .SelectMany(a => a.Attributes)
            .FirstOrDefault(attr => attr.Name.ToString().Contains("FactAttribute", StringComparison.OrdinalIgnoreCase) ||
                                     attr.Name.ToString().Contains("TheoryAttribute", StringComparison.OrdinalIgnoreCase));

        var displayNameArgument = factOrTheoryAttribute?.ArgumentList?.Arguments
            .FirstOrDefault(arg => arg.NameEquals?.Name.Identifier.ValueText == "DisplayName");

        if (displayNameArgument?.Expression is LiteralExpressionSyntax displayNameLiteral &&
            displayNameLiteral.IsKind(SyntaxKind.StringLiteralExpression))
        {
            scenarioName = displayNameLiteral.Token.ValueText;
        }

        return scenarioName;
    }

    public bool IsScenarioOutline(MethodDeclarationSyntax method)
    {
        return method.AttributeLists.SelectMany(x => x.Attributes).Select(x => x.Name.ToString())
            .Any(x => x.Contains("InlineDataAttribute"));
    }

    public IEnumerable<ExampleRow> GetExampleRows(MethodDeclarationSyntax method)
    {
        var result = new List<ExampleRow>();
        var relevantAttributes = method.AttributeLists.SelectMany(x => x.Attributes).Where(x => x.ToString().Contains("InlineDataAttribute"));

        foreach (var a in relevantAttributes.Where(x => x.ArgumentList is not null))
        {
            var pickleIndex = (a.ArgumentList!.Arguments[^2].Expression as LiteralExpressionSyntax)?.Token.ValueText ?? "-1";
            var arguments = a.ArgumentList.Arguments.Take(a.ArgumentList.Arguments.Count - 2)
                .Where(x => x.Expression is LiteralExpressionSyntax)
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