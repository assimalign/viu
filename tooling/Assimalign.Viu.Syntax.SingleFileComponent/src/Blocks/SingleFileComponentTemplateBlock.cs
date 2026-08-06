namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// A template block — the component's markup, written in the Viu template language. The content is
/// <em>not</em> parsed here; the container parse only slices, and the template compiler
/// ([V01.01.05.01]) consumes <see cref="SingleFileComponentBlock.Content"/>.
/// </summary>
public sealed record SingleFileComponentTemplateBlock : SingleFileComponentBlock
{
    /// <inheritdoc />
    public override SingleFileComponentBlockKind Kind => SingleFileComponentBlockKind.Template;
}
