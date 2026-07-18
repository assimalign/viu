namespace Assimalign.Vue.Compiler;

/// <summary>
/// A leaf expression — either a static string (a static directive argument or modifier) or a raw
/// dynamic JavaScript expression string. The C# port of Vue 3.5's <c>SimpleExpressionNode</c>
/// (<c>@vue/compiler-core</c> <c>ast.ts</c>), reduced to the members the parser sets. This build does
/// not parse expression bodies into a JavaScript AST (the <c>prefixIdentifiers</c> path), so the Babel
/// <c>ast</c> field has no counterpart here.
/// </summary>
public sealed record SimpleExpressionNode : ExpressionNode
{
    /// <summary>The expression text (for a dynamic argument, without the surrounding brackets).</summary>
    public required string Content { get; init; }

    /// <summary>Whether the content is a static string rather than a dynamic expression.</summary>
    public required bool IsStatic { get; init; }

    /// <summary>The static-ness level (see <see cref="ConstantType"/>).</summary>
    public ConstantType ConstantType { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.SimpleExpression;
}
