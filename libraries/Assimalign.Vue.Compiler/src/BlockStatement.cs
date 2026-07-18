namespace Assimalign.Vue.Compiler;

/// <summary>
/// A code-generation block statement — a sequence of statements forming a function body. The C# port of Vue
/// 3.5's <c>BlockStatement</c> (<c>@vue/compiler-core</c> <c>ast.ts</c>). In this build it is produced only
/// for the memoized <c>v-for</c> loop body (<c>v-memo</c> combined with <c>v-for</c>).
/// </summary>
public sealed record BlockStatement : SyntaxNode
{
    /// <summary>
    /// The ordered statements. Each is a literal <see cref="string"/> or a <see cref="SyntaxNode"/>, mirroring
    /// upstream's untyped body array.
    /// </summary>
    public required SyntaxList<object> Body { get; init; }

    /// <inheritdoc />
    public override NodeType NodeType => NodeType.JsBlockStatement;
}
