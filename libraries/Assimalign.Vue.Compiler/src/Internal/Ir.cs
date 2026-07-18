using System.Collections.Generic;

namespace Assimalign.Vue.Compiler;

/// <summary>
/// Factory helpers for the code-generation intermediate representation, the C# port of the <c>createXxx</c>
/// functions in Vue 3.5's <c>@vue/compiler-core</c> <c>ast.ts</c>. Synthesized nodes that never map to
/// template source carry the <see cref="LocationStub"/>.
/// </summary>
internal static class Ir
{
    /// <summary>The stub source location for synthesized nodes (upstream <c>locStub</c>).</summary>
    public static readonly SourceLocation LocationStub =
        new(new Position(0, 1, 1), new Position(0, 1, 1), string.Empty);

    /// <summary>Creates a simple expression (upstream <c>createSimpleExpression</c>).</summary>
    public static SimpleExpressionNode SimpleExpression(
        string content,
        bool isStatic = false,
        SourceLocation? location = null,
        ConstantType constantType = ConstantType.NotConstant)
        => new()
        {
            Content = content,
            IsStatic = isStatic,
            ConstantType = isStatic ? ConstantType.CanStringify : constantType,
            Location = location ?? LocationStub,
        };

    /// <summary>Creates a compound expression from ordered parts (upstream <c>createCompoundExpression</c>).</summary>
    public static CompoundExpressionNode CompoundExpression(params object[] parts)
        => new() { Parts = new SyntaxList<object>(parts), Location = LocationStub };

    /// <summary>Creates an object property with a static string key (upstream <c>createObjectProperty</c>).</summary>
    public static Property ObjectProperty(string key, SyntaxNode value)
        => new() { Key = SimpleExpression(key, true), Value = value, Location = LocationStub };

    /// <summary>Creates an object property with an expression key (upstream <c>createObjectProperty</c>).</summary>
    public static Property ObjectProperty(ExpressionNode key, SyntaxNode value)
        => new() { Key = key, Value = value, Location = LocationStub };

    /// <summary>Creates an object literal (upstream <c>createObjectExpression</c>).</summary>
    public static ObjectExpression ObjectExpression(IReadOnlyList<Property> properties, SourceLocation? location = null)
        => new() { Properties = ToList(properties), Location = location ?? LocationStub };

    /// <summary>Creates an array literal (upstream <c>createArrayExpression</c>).</summary>
    public static ArrayExpression ArrayExpression(IReadOnlyList<object> elements, SourceLocation? location = null)
        => new() { Elements = ToList(elements), Location = location ?? LocationStub };

    /// <summary>Creates a call expression (upstream <c>createCallExpression</c>).</summary>
    public static CallExpression CallExpression(object callee, IReadOnlyList<object> arguments, SourceLocation? location = null)
        => new() { Callee = callee, Arguments = ToList(arguments), Location = location ?? LocationStub };

    /// <summary>Creates a ternary conditional (upstream <c>createConditionalExpression</c>).</summary>
    public static ConditionalExpression ConditionalExpression(
        SyntaxNode test,
        SyntaxNode consequent,
        SyntaxNode alternate,
        bool newline = true)
        => new() { Test = test, Consequent = consequent, Alternate = alternate, Newline = newline, Location = LocationStub };

    /// <summary>Creates a function expression (upstream <c>createFunctionExpression</c>).</summary>
    public static FunctionExpression FunctionExpression(
        IReadOnlyList<object>? parameters,
        object? returns = null,
        bool newline = false,
        bool isSlot = false,
        SourceLocation? location = null)
        => new()
        {
            Parameters = parameters is null ? SyntaxList<object>.Empty : ToList(parameters),
            Returns = returns,
            Newline = newline,
            IsSlot = isSlot,
            Location = location ?? LocationStub,
        };

    /// <summary>Creates a block statement (upstream <c>createBlockStatement</c>).</summary>
    public static BlockStatement BlockStatement(IReadOnlyList<object> body)
        => new() { Body = ToList(body), Location = LocationStub };

    /// <summary>Creates a cache-slot expression (upstream <c>createCacheExpression</c>).</summary>
    public static CacheExpression CacheExpression(int index, SyntaxNode value, bool needPauseTracking = false, bool inVOnce = false)
        => new()
        {
            Index = index,
            Value = value,
            NeedPauseTracking = needPauseTracking,
            InVOnce = inVOnce,
            Location = LocationStub,
        };

    private static SyntaxList<T> ToList<T>(IReadOnlyList<T> items)
        where T : class
    {
        if (items.Count == 0)
        {
            return SyntaxList<T>.Empty;
        }

        var array = new T[items.Count];
        for (var index = 0; index < items.Count; index++)
        {
            array[index] = items[index];
        }

        return new SyntaxList<T>(array);
    }
}
