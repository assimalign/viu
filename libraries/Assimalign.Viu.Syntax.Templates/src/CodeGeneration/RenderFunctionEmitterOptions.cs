namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// Configures <see cref="RenderFunctionEmitter"/>. The C# counterpart of the codegen-relevant members of
/// Vue 3.5's <c>CodegenOptions</c> (<c>@vue/compiler-core</c> <c>options.ts</c>), reduced to what the C#
/// emission actually varies on: Vue's <c>mode</c>/<c>prefixIdentifiers</c>/<c>runtimeModuleName</c> options
/// select between JavaScript module/function preambles, which have no C# counterpart — the composition
/// root (the source generator, [V01.01.06.02]) owns the method declaration and the file-level
/// <c>using static</c> helper import instead.
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
    /// The text of one indentation level. Defaults to four spaces (the repository C# convention; upstream
    /// codegen.ts indents two — a purely cosmetic divergence).
    /// </summary>
    public string IndentText { get; set; } = "    ";
}
