using Microsoft.CodeAnalysis.CSharp.Syntax;
using Reqnroll.LanguageServer.Models.FeatureCsParser;

namespace Reqnroll.LanguageServer.Services.GeneratedCsParser;

public class MsTestFeatureCsParser : BaseCsParser, IFrameworkSpecificFeatureCsParser
{
    public string? GetFeatureName(ClassDeclarationSyntax classNode)
    {
        return FindFeatureNameInMethodsByNames(
            classNode,
            ["Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute"],
            ["Microsoft.VisualStudio.TestTools.UnitTesting.TestPropertyAttribute"]);
    }

    public string[] GetTags(MethodDeclarationSyntax methodNode)
    {
        return GetTagsByAttributeNames(methodNode, ["Microsoft.VisualStudio.TestTools.UnitTesting.TestCategoryAttribute"
        ]);
    }

    public string? GetScenarioName(MethodDeclarationSyntax methodNode)
    {
        var attrs = GetMethodAttributes(methodNode);
        return GetFirstAttributeArgumentValue(attrs, attr => attr.Name.ToString().Contains("Description", StringComparison.OrdinalIgnoreCase));
    }

    public bool IsScenarioOutline(MethodDeclarationSyntax method)
    {
        return HasAttributeWithNameContaining(method, "DataRowAttribute");
    }

    public IEnumerable<ExampleRow> GetExampleRows(MethodDeclarationSyntax method)
    {
        return GetExampleRowsByAttributeNames(method, ["DataRowAttribute"], 3).ToList();
    }
}