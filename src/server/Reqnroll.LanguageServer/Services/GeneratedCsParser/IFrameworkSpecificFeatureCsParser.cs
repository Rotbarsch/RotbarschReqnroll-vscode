using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Reqnroll.LanguageServer.Services.GeneratedCsParser;

public interface IFrameworkSpecificFeatureCsParser
{
    public string? GetFeatureName(ClassDeclarationSyntax classNode);
    public string? GetScenarioName(MethodDeclarationSyntax methodNode);
}