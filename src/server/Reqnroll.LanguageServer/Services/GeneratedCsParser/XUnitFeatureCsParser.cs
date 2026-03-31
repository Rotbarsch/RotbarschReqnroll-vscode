using Microsoft.CodeAnalysis.CSharp.Syntax;
using Reqnroll.LanguageServer.Models.FeatureCsParser;

namespace Reqnroll.LanguageServer.Services.GeneratedCsParser;

public class XUnitFeatureCsParser : BaseCsParser, IFrameworkSpecificFeatureCsParser
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

            if (traitAttribute?.ArgumentList?.Arguments is { Count: >= 2 } args)
            {
                var name = ResolveExpressionSyntax(args[0].Expression);
                var value = ResolveExpressionSyntax(args[1].Expression);
                if (name == "FeatureTitle" && value is not null)
                {
                    featureName = value;
                    break;
                }
            }
        }

        return featureName;
    }

    public string[] GetTags(MethodDeclarationSyntax methodNode)
    {
        return methodNode.AttributeLists.SelectMany(x => x.Attributes)
            .Where(x => x.Name.ToString().Contains("TraitAttribute") &&
                        x.ArgumentList?.Arguments is { Count: > 1 } &&
                        ResolveExpressionSyntax(x.ArgumentList.Arguments[0].Expression) == "Category" &&
                        ResolveExpressionSyntax(x.ArgumentList.Arguments[1].Expression) is not null)
            .Select(x => ResolveExpressionSyntax(x.ArgumentList!.Arguments[1].Expression)!)
            .ToArray();
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

        var expression = displayNameArgument?.Expression;
        return expression is null ? null : ResolveExpressionSyntax(expression);
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
            var args = a.ArgumentList!.Arguments;
            var pickleIndex = ResolveExpressionSyntax(args[^2].Expression) ?? "-1";
            var arguments = args.Take(args.Count - 2)
                .Select(x => ResolveExpressionSyntax(x.Expression))
                .Where(s => s is not null)
                .Select(s => s!);

            result.Add(new ExampleRow
            {
                Arguments = string.Join(",", arguments),
                PickleIndex = int.Parse(pickleIndex)
            });
        }

        return result;
    }
}