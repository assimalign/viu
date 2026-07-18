using System.Collections.Generic;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Microsoft.CodeAnalysis.CSharpExtensions;

namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// Extracts the identifier names an alias expression <i>declares</i> — the value/key/index of a <c>v-for</c>
/// and the destructured props of a <c>v-slot</c> — so the transform can register them in the template scope.
/// The C# analogue of the parameter walk Vue 3.5 performs in <c>processExpression(..., asParams = true)</c>
/// and feeds to <c>addIdentifiers</c> (<c>@vue/compiler-core</c> <c>transformExpression.ts</c>/<c>babelUtils.ts</c>).
/// </summary>
/// <remarks>
/// Because template aliases are C#, this handles the C# binding forms — a plain identifier (<c>item</c>), a
/// tuple (<c>(value, index)</c>), and a deconstruction designation (<c>var (a, b)</c>) — and falls back to a
/// lenient identifier-token scan for shapes Roslyn cannot parse as an expression (for example a JavaScript-style
/// <c>{ a, b }</c> destructure), so a malformed alias still contributes names to the scope rather than throwing.
/// </remarks>
internal static class IdentifierExtraction
{
    /// <summary>Collects the identifier names <paramref name="content"/> declares.</summary>
    /// <param name="content">The alias expression text.</param>
    public static IReadOnlyList<string> CollectDeclaredIdentifiers(string content)
    {
        var names = new List<string>();
        if (string.IsNullOrWhiteSpace(content))
        {
            return names;
        }

        var expression = SyntaxFactory.ParseExpression(content);
        if (!HasError(expression))
        {
            CollectFromDesignationOrExpression(expression, names);
            if (names.Count > 0)
            {
                return names;
            }
        }

        // Lenient fallback: gather identifier tokens that are not member accesses (`a.b` contributes only `a`).
        ScanTokens(content, names);
        return names;
    }

    private static void CollectFromDesignationOrExpression(ExpressionSyntax expression, List<string> names)
    {
        switch (expression)
        {
            case IdentifierNameSyntax identifier:
                AddName(names, identifier.Identifier.ValueText);
                break;
            case ParenthesizedExpressionSyntax parenthesized:
                CollectFromDesignationOrExpression(parenthesized.Expression, names);
                break;
            case TupleExpressionSyntax tuple:
                foreach (var argument in tuple.Arguments)
                {
                    CollectFromDesignationOrExpression(argument.Expression, names);
                }

                break;
            case DeclarationExpressionSyntax declaration:
                CollectFromDesignation(declaration.Designation, names);
                break;
        }
    }

    private static void CollectFromDesignation(VariableDesignationSyntax designation, List<string> names)
    {
        switch (designation)
        {
            case SingleVariableDesignationSyntax single:
                AddName(names, single.Identifier.ValueText);
                break;
            case ParenthesizedVariableDesignationSyntax parenthesized:
                foreach (var nested in parenthesized.Variables)
                {
                    CollectFromDesignation(nested, names);
                }

                break;
        }
    }

    private static void ScanTokens(string content, List<string> names)
    {
        var previousWasDot = false;
        foreach (var token in SyntaxFactory.ParseTokens(content))
        {
            if (token.IsKind(SyntaxKind.IdentifierToken))
            {
                if (!previousWasDot)
                {
                    AddName(names, token.ValueText);
                }

                previousWasDot = false;
            }
            else
            {
                previousWasDot = token.IsKind(SyntaxKind.DotToken);
            }
        }
    }

    private static void AddName(List<string> names, string name)
    {
        if (name.Length > 0 && !names.Contains(name))
        {
            names.Add(name);
        }
    }

    private static bool HasError(ExpressionSyntax node)
    {
        foreach (var diagnostic in node.GetDiagnostics())
        {
            if (diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            {
                return true;
            }
        }

        return false;
    }
}
