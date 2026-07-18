namespace Assimalign.Vue.Compiler;

/// <summary>
/// A directive such as <c>v-if</c>, <c>:class</c>, <c>@click</c>, or <c>#slot</c>. The C# port of
/// Vue 3.5's <c>DirectiveNode</c> (<c>@vue/compiler-core</c> <c>ast.ts</c>). The <c>:</c>/<c>@</c>/<c>#</c>
/// shorthands are resolved into <see cref="Name"/> (<c>bind</c>/<c>on</c>/<c>slot</c>).
/// </summary>
public sealed record DirectiveNode : PropertyNode
{
    /// <summary>The normalized directive name without prefix or shorthand, e.g. <c>bind</c> or <c>on</c>.</summary>
    public required string Name { get; init; }

    /// <summary>The raw attribute name preserving shorthand, argument, and modifiers, as authored.</summary>
    public string? RawName { get; init; }

    /// <summary>The directive argument, e.g. the <c>class</c> in <c>:class</c>, or <see langword="null"/>.</summary>
    public ExpressionNode? Argument { get; init; }

    /// <summary>The modifiers, e.g. the <c>stop</c> in <c>@click.stop</c>, in source order.</summary>
    public required SyntaxList<SimpleExpressionNode> Modifiers { get; init; }

    /// <summary>The directive expression (its value), or <see langword="null"/> when it has none.</summary>
    public ExpressionNode? Expression { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.Directive;
}
