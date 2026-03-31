using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Reqnroll.LanguageServer.Services.GeneratedCsParser;

public abstract class BaseCsParser
{
    protected string? ResolveExpressionSyntax(ExpressionSyntax argExpression)
    {
        switch (argExpression)
        {
            case LiteralExpressionSyntax literalExpressionSyntax:
                return literalExpressionSyntax.Token.ValueText;
            case BinaryExpressionSyntax binaryExpression:
            {
                var left = ResolveExpressionSyntax(binaryExpression.Left);
                var right = ResolveExpressionSyntax(binaryExpression.Right);
                if (binaryExpression.IsKind(SyntaxKind.AddExpression))
                {
                    return left + right;
                }
                break;
            }
        }

        return null;
    }

    protected IEnumerable<AttributeSyntax> GetAttributesWithAnyNameContaining(MethodDeclarationSyntax method, IEnumerable<string> names, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        return GetMethodAttributes(method).Where(a => names.Any(n => a.Name.ToString().Contains(n, comparison)));
    }

    protected string[] GetTagsByAttributeNames(MethodDeclarationSyntax method, IEnumerable<string> attributeNames, int argIndex = 0, Func<AttributeSyntax, bool>? extraPredicate = null)
    {
        var attrs = GetMethodAttributes(method)
            .Where(a => attributeNames.Any(n => a.Name.ToString().Contains(n)))
            .Where(a => a.ArgumentList?.Arguments is not null);

        if (extraPredicate is not null)
            attrs = attrs.Where(extraPredicate);

        return attrs.Select(a => GetAttributeArgumentValue(a, argIndex)).Where(s => s is not null).Select(s => s!).ToArray();
    }

    protected bool HasAttributeWithAnyNameContaining(MethodDeclarationSyntax method, IEnumerable<string> names, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        return GetMethodAttributes(method).Any(a => names.Any(n => a.Name.ToString().Contains(n, comparison)));
    }

    protected IEnumerable<Reqnroll.LanguageServer.Models.FeatureCsParser.ExampleRow> GetExampleRowsByAttributeNames(MethodDeclarationSyntax method, IEnumerable<string> attributeNames, int trailingArgs)
    {
        var relevantAttributes = GetMethodAttributes(method).Where(a => attributeNames.Any(n => a.ToString().Contains(n)));
        return BuildExampleRows(relevantAttributes, trailingArgs);
    }

    protected string? FindFeatureNameInMethodsByNames(
        ClassDeclarationSyntax classNode,
        IEnumerable<string> testMethodAttributeNames,
        IEnumerable<string> featureAttributeNames,
        int keyArgIndex = 0,
        int valueArgIndex = 1,
        string expectedKey = "FeatureTitle")
    {
        return FindFeatureNameInMethods(
            classNode,
            attr => testMethodAttributeNames.Any(n => attr.Name.ToString().Contains(n, StringComparison.OrdinalIgnoreCase)),
            attr => featureAttributeNames.Any(n => attr.Name.ToString().Contains(n, StringComparison.OrdinalIgnoreCase)),
            keyArgIndex,
            valueArgIndex,
            expectedKey);
    }

    protected string? FindFeatureNameInMethods(
        ClassDeclarationSyntax classNode,
        Func<AttributeSyntax, bool> isTestMethodAttribute,
        Func<AttributeSyntax, bool> isFeatureAttribute,
        int keyArgIndex = 0,
        int valueArgIndex = 1,
        string expectedKey = "FeatureTitle")
    {
        foreach (var method in classNode.Members.OfType<MethodDeclarationSyntax>())
        {
            var attributes = GetMethodAttributes(method).ToList();

            if (!attributes.Any(isTestMethodAttribute))
                continue;

            var featureAttr = attributes.FirstOrDefault(isFeatureAttribute);

            var args = featureAttr?.ArgumentList?.Arguments;
            if (args is null) continue;
            var argsValue = args.Value;
            if (argsValue.Count <= valueArgIndex) continue;

            var key = ResolveExpressionSyntax(argsValue[keyArgIndex].Expression);
            var value = ResolveExpressionSyntax(argsValue[valueArgIndex].Expression);

            if (key == expectedKey && value is not null)
                return value;
        }

        return null;
    }

    protected IEnumerable<AttributeSyntax> GetMethodAttributes(MethodDeclarationSyntax method)
    {
        return method.AttributeLists.SelectMany(a => a.Attributes);
    }

    protected IEnumerable<AttributeSyntax> GetClassAttributes(ClassDeclarationSyntax classNode)
    {
        return classNode.AttributeLists.SelectMany(a => a.Attributes);
    }

    protected IEnumerable<AttributeSyntax> GetAttributesContaining(MethodDeclarationSyntax method, string substring, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        return GetMethodAttributes(method).Where(a => a.ToString().Contains(substring, comparison));
    }

    protected bool HasAttributeWithNameContaining(MethodDeclarationSyntax method, string substring, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        return GetMethodAttributes(method).Select(a => a.Name.ToString()).Any(n => n.Contains(substring, comparison));
    }

    protected string? GetAttributeArgumentValue(AttributeSyntax attr, int index)
    {
        var args = attr.ArgumentList?.Arguments;
        if (args is null || args.Value.Count <= index) return null;
        return ResolveExpressionSyntax(args.Value[index].Expression);
    }

    protected string? GetFirstAttributeArgumentValue(IEnumerable<AttributeSyntax> attributes, Func<AttributeSyntax, bool> predicate, int index = 0)
    {
        var attr = attributes.FirstOrDefault(predicate);
        return attr is null ? null : GetAttributeArgumentValue(attr, index);
    }

    protected string? GetNamedArgumentValue(AttributeSyntax attr, string name)
    {
        var named = attr.ArgumentList?.Arguments.FirstOrDefault(a => a.NameEquals?.Name.Identifier.ValueText == name);
        return named is null ? null : ResolveExpressionSyntax(named.Expression);
    }

    protected string[] GetAttributeArgumentValues(MethodDeclarationSyntax method, Func<AttributeSyntax, bool> predicate, int argIndex = 0)
    {
        return method.AttributeLists.SelectMany(a => a.Attributes)
            .Where(predicate)
            .Where(x => x.ArgumentList?.Arguments != null)
            .Select(x => GetAttributeArgumentValue(x, argIndex))
            .Where(s => s is not null)
            .Select(s => s!)
            .ToArray();
    }

    protected IEnumerable<Reqnroll.LanguageServer.Models.FeatureCsParser.ExampleRow> BuildExampleRows(IEnumerable<AttributeSyntax> relevantAttributes, int trailingArgs)
    {
        var result = new List<Reqnroll.LanguageServer.Models.FeatureCsParser.ExampleRow>();

        foreach (var a in relevantAttributes.Where(x => x.ArgumentList is not null))
        {
            var args = a.ArgumentList!.Arguments;
            var pickleIndex = ResolveExpressionSyntax(args[^trailingArgs].Expression) ?? "-1";
            var arguments = args.Take(args.Count - trailingArgs)
                .Select(x => ResolveExpressionSyntax(x.Expression))
                .Where(s => s is not null)
                .Select(s => s!);

            result.Add(new Reqnroll.LanguageServer.Models.FeatureCsParser.ExampleRow
            {
                Arguments = string.Join(",", arguments),
                PickleIndex = int.Parse(pickleIndex)
            });
        }

        return result;
    }
}