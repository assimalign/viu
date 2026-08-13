namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// Configures <see cref="RenderFunctionEmitter"/>'s target profile and statement indentation. The
/// emitter produces a method <em>body</em>, nothing more: a composition root owns the method declaration,
/// its profile-specific parameters, and the enclosing partial class. Generated runtime references are
/// fully qualified, so no import contract is required. Specified by <c>[SFC-CG-1]</c>,
/// <c>[SFC-CG-2]</c>, and <c>[SSR-COMPILE-1]</c>.
/// </summary>
public sealed class RenderFunctionEmitterOptions
{
    /// <summary>
    /// Gets or sets the generated body's target profile. Defaults to the host-neutral virtual-node
    /// tree, preserving the existing interactive compiler path.
    /// </summary>
    /// <remarks>
    /// <see cref="RenderFunctionTargetProfile.ServerMarkup"/> produces an asynchronous body whose
    /// enclosing method supplies <c>component</c>, <c>frame</c>, <c>state</c>, and <c>parent</c> locals.
    /// The template must have been transformed with <see cref="TransformOptions.IsServerRendering"/>
    /// so browser-only operations never enter fallback code. Specified by
    /// <c>[SSR-COMPILE-1]</c> and <c>[SSR-COMPILE-2]</c>.
    /// </remarks>
    public RenderFunctionTargetProfile TargetProfile { get; set; }

    /// <summary>
    /// The indentation level the emitted statements start at (the number of <see cref="IndentText"/>
    /// repetitions prefixed to each line). The generator passes the nesting depth of the render method's
    /// body so the emitted code sits correctly inside the generated partial class. Defaults to 0.
    /// </summary>
    public int IndentLevel { get; set; }

    /// <summary>
    /// The text of one indentation level. Defaults to four spaces, the repository C# convention.
    /// </summary>
    public string IndentText { get; set; } = "    ";
}
