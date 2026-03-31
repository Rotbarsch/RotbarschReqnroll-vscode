using Microsoft.CodeAnalysis.CSharp.Syntax;
using Reqnroll.LanguageServer.Models.FeatureCsParser;

namespace Reqnroll.LanguageServer.Services.GeneratedCsParser;

public interface IFrameworkSpecificFeatureCsParser
{
    string? GetFeatureName(ClassDeclarationSyntax classNode);
    string[] GetTags(MethodDeclarationSyntax methodNode);
    string? GetScenarioName(MethodDeclarationSyntax methodNode);
    bool IsScenarioOutline(MethodDeclarationSyntax method);
    IEnumerable<ExampleRow> GetExampleRows(MethodDeclarationSyntax method);
}