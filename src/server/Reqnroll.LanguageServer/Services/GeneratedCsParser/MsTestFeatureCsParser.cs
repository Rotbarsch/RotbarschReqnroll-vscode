using Microsoft.CodeAnalysis.CSharp.Syntax;
using Reqnroll.LanguageServer.Models.FeatureCsParser;

namespace Reqnroll.LanguageServer.Services.GeneratedCsParser;

public class MsTestFeatureCsParser : BaseCsParser, IFrameworkSpecificFeatureCsParser
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

            if (testPropertyAttribute?.ArgumentList?.Arguments.Count >= 2)
            {
                var attributeArgumentValues =
                    testPropertyAttribute?.ArgumentList?.Arguments.Select(a => ResolveExpressionSyntax(a.Expression)).ToList()!;

                if (attributeArgumentValues[0] == "FeatureTitle")
                {
                    featureName = attributeArgumentValues[1];
                    break;
                }
            }
        }

        return featureName;
    }

    public string[] GetTags(MethodDeclarationSyntax methodNode)
    {
        return methodNode.AttributeLists.SelectMany(x => x.Attributes)
            .Where(x => x.Name.ToString().Contains("Microsoft.VisualStudio.TestTools.UnitTesting.TestCategoryAttribute"))
            .Where(x=>x.ArgumentList?.Arguments != null)
            .Select(x=>x.ArgumentList!.Arguments.Select(a=>ResolveExpressionSyntax(a.Expression)))
            .Select(x =>  x.First()!)
            .ToArray();
    }

    public string? GetScenarioName(MethodDeclarationSyntax methodNode)
    {
        var scenarioDescriptionAttribute = methodNode.AttributeLists
            .SelectMany(a => a.Attributes)
            .FirstOrDefault(attr => attr.Name.ToString().Contains("Description", StringComparison.OrdinalIgnoreCase));

        if (scenarioDescriptionAttribute is null) return null;

        var scenarioExpression = scenarioDescriptionAttribute?.ArgumentList?.Arguments.FirstOrDefault()?.Expression;

        if(scenarioExpression is null) return null;

        return ResolveExpressionSyntax(scenarioExpression);
    }

    public bool IsScenarioOutline(MethodDeclarationSyntax method)
    {
        return method.AttributeLists.SelectMany(x => x.Attributes).Select(x => x.Name.ToString())
            .Any(x => x.Contains("DataRowAttribute"));
    }

    public IEnumerable<ExampleRow> GetExampleRows(MethodDeclarationSyntax method)
    {
        var result = new List<ExampleRow>();
        var relevantAttributes = method.AttributeLists.SelectMany(x => x.Attributes).Where(x => x.ToString().Contains("DataRowAttribute"));

        foreach (var a in relevantAttributes.Where(x => x.ArgumentList is not null))
        {
            var pickleIndex = (a.ArgumentList!.Arguments[^3].Expression as LiteralExpressionSyntax)?.Token.ValueText ?? "-1";
            var arguments = a.ArgumentList.Arguments.Take(a.ArgumentList.Arguments.Count - 3)
                .Select(x => ResolveExpressionSyntax(x.Expression));

            result.Add(new ExampleRow
            {
                Arguments = string.Join(",", arguments),
                PickleIndex = int.Parse(pickleIndex)
            });
        }

        return result;
    }
}