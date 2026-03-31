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
}