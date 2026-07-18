namespace Assimalign.Vue.Syntax.SingleFileComponent;

/// <summary>
/// A <c>@style</c> block — the component's CSS. Mirrors Vue 3.5's <c>SFCStyleBlock</c>
/// (<c>@vue/compiler-sfc</c>), including the <c>scoped</c> and <c>module</c> options. A <c>.viu</c> file
/// may contain several <c>@style</c> blocks (as Vue allows several <c>&lt;style&gt;</c> tags). See
/// https://vuejs.org/api/sfc-spec.html#style and https://vuejs.org/api/sfc-css-features.html.
/// </summary>
public sealed record SingleFileComponentStyleBlock : SingleFileComponentBlock
{
    /// <inheritdoc />
    public override SingleFileComponentBlockKind Kind => SingleFileComponentBlockKind.Style;

    /// <summary>
    /// Whether the <c>scoped</c> option is present — the CSS applies only to the component's own
    /// elements. Mirrors Vue's <c>&lt;style scoped&gt;</c>
    /// (https://vuejs.org/api/sfc-css-features.html#scoped-css).
    /// </summary>
    public bool Scoped => HasOption("scoped");

    /// <summary>
    /// Whether the <c>module</c> option is present — the CSS compiles to CSS Modules. Mirrors Vue's
    /// <c>&lt;style module&gt;</c> (https://vuejs.org/api/sfc-css-features.html#css-modules).
    /// </summary>
    public bool IsModule => HasOption("module");

    /// <summary>
    /// The name given as <c>module="name"</c>, or <see langword="null"/> when <c>module</c> is absent or
    /// valueless. Mirrors the string form of Vue's <c>module</c> attribute.
    /// </summary>
    public string? ModuleName => GetOptionValue("module");
}
