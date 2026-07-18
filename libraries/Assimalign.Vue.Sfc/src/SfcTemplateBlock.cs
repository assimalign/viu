namespace Assimalign.Vue.Sfc;

/// <summary>
/// An <c>@template</c> block — the component's markup. Mirrors Vue 3.5's <c>SFCTemplateBlock</c>
/// (<c>@vue/compiler-sfc</c>). The content is standard Vue template syntax and is <em>not</em> parsed
/// here; the template compiler ([V01.01.05.01]) consumes <see cref="SfcBlock.Content"/>. See
/// https://vuejs.org/api/sfc-spec.html#template.
/// </summary>
public sealed record SfcTemplateBlock : SfcBlock
{
    /// <inheritdoc />
    public override SfcBlockKind Kind => SfcBlockKind.Template;
}
