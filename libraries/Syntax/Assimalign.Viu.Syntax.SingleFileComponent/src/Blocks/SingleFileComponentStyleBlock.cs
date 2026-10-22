namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// A style block — the component's CSS, carrying the <c>scoped</c> and <c>module</c> options that
/// select the compile-time rewrite applied to it (<c>[STY-1]</c>, <c>[STY-2]</c>). Unlike template and
/// script, style is <em>not</em> limited to one per component: several blocks may declare different
/// options — one scoped, one global, one module — and all of them contribute.
/// </summary>
public sealed record SingleFileComponentStyleBlock : SingleFileComponentBlock
{
    /// <inheritdoc />
    public override SingleFileComponentBlockKind Kind => SingleFileComponentBlockKind.Style;

    /// <summary>
    /// Whether the <c>scoped</c> option is present — every selector is rewritten to the component's
    /// scope identifier, so the CSS applies only to the component's own elements (<c>[STY-1]</c>).
    /// </summary>
    public bool Scoped => HasOption("scoped");

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
