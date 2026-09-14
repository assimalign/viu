namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// A style block containing ordinary component CSS or a CSS module (<c>[STY-2]</c>).
/// Unlike template and script, style is not limited to one block per component: all blocks contribute
/// in source order. Unsupported options remain available as tokens for located diagnostics
/// ([V01.01.06.17]).
/// </summary>
public sealed record SingleFileComponentStyleBlock : SingleFileComponentBlock
{
    /// <inheritdoc />
    public override SingleFileComponentBlockKind Kind => SingleFileComponentBlockKind.Style;

    /// <summary>
    /// Whether the <c>module</c> option is present — local class names are hashed and exposed through a
    /// generated compile-time accessor class rather than written literally (<c>[STY-2]</c>).
    /// </summary>
    public bool IsModule => HasOption("module");

    /// <summary>
    /// The name given as <c>module="name"</c>, or <see langword="null"/> when <c>module</c> is absent or
    /// valueless. A named module compiles to its own pascal-cased accessor class, so one component can
    /// carry several independent module blocks.
    /// </summary>
    public string? ModuleName => GetOptionValue("module");
}
