using Microsoft.CodeAnalysis.CSharp.Syntax;
using Reqnroll.LanguageServer.Models.FeatureCsParser;

namespace Reqnroll.LanguageServer.Services.GeneratedCsParser;

public class NUnitFeatureCsParser : BaseCsParser, IFrameworkSpecificFeatureCsParser
{
    public string? GetFeatureName(ClassDeclarationSyntax classNode)
    {
        var featureDescriptionAttribute = GetClassAttributes(classNode)
            .FirstOrDefault(attr => attr.Name.ToString().Contains("Description", StringComparison.OrdinalIgnoreCase));

        var expression = featureDescriptionAttribute?.ArgumentList?.Arguments.FirstOrDefault()?.Expression;
        return expression is null ? null : ResolveExpressionSyntax(expression);
    }

    public string[] GetTags(MethodDeclarationSyntax methodNode)
    {
        return GetTagsByAttributeNames(methodNode, ["NUnit.Framework.CategoryAttribute"]);
    }

    public string? GetScenarioName(MethodDeclarationSyntax methodNode)
    {
        var attrs = GetMethodAttributes(methodNode);
        return GetFirstAttributeArgumentValue(attrs, attr => attr.Name.ToString().Contains("Description", StringComparison.OrdinalIgnoreCase));
    }

    public bool IsScenarioOutline(MethodDeclarationSyntax method)
    {
        return HasAttributeWithNameContaining(method, "TestCaseAttribute");
    }

    public IEnumerable<ExampleRow> GetExampleRows(MethodDeclarationSyntax method)
    {
        return GetExampleRowsByAttributeNames(method, ["TestCaseAttribute"], 2).ToList();
    }
}