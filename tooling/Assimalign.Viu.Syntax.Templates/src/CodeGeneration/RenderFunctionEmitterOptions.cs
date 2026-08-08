namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// Configures <see cref="RenderFunctionEmitter"/>. The surface is deliberately tiny — indentation only —
/// because the emitter produces a method <em>body</em>, nothing more: the composition root (the source
/// generator, [V01.01.06.02]) owns the method declaration, its component and frame parameters, and the
/// enclosing partial class. Generated runtime references are fully qualified, so no import contract is
/// required. Specified by <c>[SFC-CG-1]</c> and <c>[SFC-CG-2]</c>.
/// </summary>
public sealed class RenderFunctionEmitterOptions
{
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
