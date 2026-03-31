using Microsoft.CodeAnalysis.CSharp.Syntax;
using Reqnroll.LanguageServer.Models.FeatureCsParser;

namespace Reqnroll.LanguageServer.Services.GeneratedCsParser;

public class XUnitFeatureCsParser : BaseCsParser, IFrameworkSpecificFeatureCsParser
{
    public string? GetFeatureName(ClassDeclarationSyntax classNode)
    {
        return FindFeatureNameInMethods(
            classNode,
            attr => attr.Name.ToString().Contains("FactAttribute", StringComparison.OrdinalIgnoreCase) ||
                    attr.Name.ToString().Contains("TheoryAttribute", StringComparison.OrdinalIgnoreCase),
            attr => attr.Name.ToString().Contains("TraitAttribute", StringComparison.OrdinalIgnoreCase));
    }

    public string[] GetTags(MethodDeclarationSyntax methodNode)
    {
        return GetTagsByAttributeNames(methodNode, ["TraitAttribute"], 1, attr =>
            attr.ArgumentList?.Arguments is { Count: > 1 } &&
            ResolveExpressionSyntax(attr.ArgumentList.Arguments[0].Expression) == "Category" &&
            ResolveExpressionSyntax(attr.ArgumentList.Arguments[1].Expression) is not null);
    }

    public string? GetScenarioName(MethodDeclarationSyntax methodNode)
    {
        // The method should have either an attribute whose name contains "FactAttribute" or "TheoryAttribute".
        // This attribute should have a named argument "DisplayName".
        // Return the value of that argument.
        return GetMethodAttributes(methodNode)
            .FirstOrDefault(attr => attr.Name.ToString().Contains("FactAttribute", StringComparison.OrdinalIgnoreCase) ||
                                     attr.Name.ToString().Contains("TheoryAttribute", StringComparison.OrdinalIgnoreCase)) is AttributeSyntax factOrTheoryAttribute
            ? GetNamedArgumentValue(factOrTheoryAttribute, "DisplayName")
            : null;
    }

    public bool IsScenarioOutline(MethodDeclarationSyntax method)
    {
        return HasAttributeWithNameContaining(method, "InlineDataAttribute");
    }

    public IEnumerable<ExampleRow> GetExampleRows(MethodDeclarationSyntax method)
    {
        return GetExampleRowsByAttributeNames(method, ["InlineDataAttribute"], 2).ToList();
    }
}