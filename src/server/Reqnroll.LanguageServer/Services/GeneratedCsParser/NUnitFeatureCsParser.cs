using Microsoft.CodeAnalysis.CSharp.Syntax;
using Reqnroll.LanguageServer.Models.FeatureCsParser;

namespace Reqnroll.LanguageServer.Services.GeneratedCsParser;

public class NUnitFeatureCsParser : BaseCsParser,IFrameworkSpecificFeatureCsParser
{
    public string? GetFeatureName(ClassDeclarationSyntax classNode)
    {
        var featureDescriptionAttribute = classNode.AttributeLists
            .SelectMany(a => a.Attributes)
            .FirstOrDefault(attr => attr.Name.ToString().Contains("Description", StringComparison.OrdinalIgnoreCase));

        var expression = featureDescriptionAttribute?.ArgumentList?.Arguments.FirstOrDefault()?.Expression;
        return expression is null ? null : ResolveExpressionSyntax(expression);
    }

    public string[] GetTags(MethodDeclarationSyntax methodNode)
    {
        return methodNode.AttributeLists.SelectMany(x => x.Attributes)
            .Where(x => x.Name.ToString().Contains("NUnit.Framework.CategoryAttribute") &&
                        x.ArgumentList?.Arguments is { Count: > 0 } &&
                        ResolveExpressionSyntax(x.ArgumentList.Arguments[0].Expression) is not null)
            .Select(x => ResolveExpressionSyntax(x.ArgumentList!.Arguments[0].Expression)!)
            .ToArray();
    }

    public string? GetScenarioName(MethodDeclarationSyntax methodNode)
    {
        var scenarioDescriptionAttribute = methodNode.AttributeLists
            .SelectMany(a => a.Attributes)
            .FirstOrDefault(attr => attr.Name.ToString().Contains("Description", StringComparison.OrdinalIgnoreCase));

        var expression = scenarioDescriptionAttribute?.ArgumentList?.Arguments.FirstOrDefault()?.Expression;
        return expression is null ? null : ResolveExpressionSyntax(expression);
    }

    public bool IsScenarioOutline(MethodDeclarationSyntax method)
    {
        return method.AttributeLists.SelectMany(x => x.Attributes).Select(x => x.Name.ToString())
            .Any(x => x.Contains("TestCaseAttribute"));
    }

    public IEnumerable<ExampleRow> GetExampleRows(MethodDeclarationSyntax method)
    {
        var result = new List<ExampleRow>();
        var relevantAttributes = method.AttributeLists.SelectMany(x => x.Attributes).Where(x => x.ToString().Contains("TestCaseAttribute"));

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