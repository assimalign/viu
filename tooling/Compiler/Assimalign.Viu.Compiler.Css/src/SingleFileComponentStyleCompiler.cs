using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using Assimalign.Viu.Syntax;
using Assimalign.Viu.Syntax.Css;
using Assimalign.Viu.Syntax.SingleFileComponent;

namespace Assimalign.Viu.Compiler.Css;

/// <summary>
/// Compiles a single-file component's style blocks into extracted CSS — the shared, deterministic core
/// reused by <b>both</b> build-time hosts ([V01.01.12.12]/[V01.01.06.09]). Each block is run through the
/// CSS-Modules class rename (<c>module</c> blocks, <see cref="CssModuleRewriter"/>), the <c>v-bind()</c>
/// custom-property rewrite (<see cref="CssBindingRewriter"/>), and then serialized through
/// <see cref="CssStylesheetWriter"/> when rewritten. Untouched ordinary blocks pass through verbatim;
/// all blocks are concatenated in source order ([V01.01.06.17]).
/// <para>
/// This is the exact logic that produced the generator's <c>ExtractedStyles</c> constant when it lived inside
/// <c>Assimalign.Viu.Generators.Syntax</c>; it was lifted here unchanged so the generator and the
/// <c>ViuBundleCss</c> MSBuild task run <em>one</em> deterministic implementation over the same inputs. That
/// single-path design is what makes the emitted constant and the physical bundle byte-identical — there is no
/// second, divergent generation path (see
/// <c>tooling/Compiler/Assimalign.Viu.Compiler.Css/docs/DESIGN.md</c>). No I/O, no reflection, no
/// dynamic codegen; recoverable (malformed <c>v-bind()</c> surfaces as a diagnostic, never throws) — the only
/// expected exception is <see cref="OperationCanceledException"/>.
/// </para>
/// </summary>
public static class SingleFileComponentStyleCompiler
{
    /// <summary>
    /// Parses <paramref name="viuText"/> with <paramref name="parser"/>, resolves the CSS name salt from the path,
    /// and compiles the component's style blocks. The convenience entry point for the
    /// <c>ViuBundleCss</c> task, which starts from raw file text; the generator uses
    /// <see cref="Compile(AggregateSyntaxParserResult{SingleFileComponentBlock}, string, CancellationToken)"/> directly
    /// because it already holds the shared parse. Both routes run the identical compilation.
    /// </summary>
    /// <param name="parser">The composed parser from <see cref="SingleFileComponentParserFactory.Create"/>.</param>
    /// <param name="viuText">The <c>.viu</c> file's full text.</param>
    /// <param name="filePath">The <c>.viu</c> file path (drives the CSS name hash).</param>
    /// <param name="projectDirectory">The consuming project's directory, or <see langword="null"/> when unknown.</param>
    /// <param name="cancellationToken">Cancels the compilation.</param>
    /// <returns>The component's style compilation.</returns>
    public static SingleFileComponentStyleCompilation CompileFile(
        SingleFileComponentSyntaxParser parser,
        string viuText,
        string filePath,
        string? projectDirectory,
        CancellationToken cancellationToken = default)
    {
        if (parser is null)
        {
            throw new ArgumentNullException(nameof(parser));
        }

        var parse = parser.ParseComponent(viuText, cancellationToken);
        var localHashSalt = CssComponentHash.Resolve(filePath, projectDirectory);
        return Compile(parse, localHashSalt, cancellationToken);
    }

    /// <summary>
    /// Compiles the style blocks in an already-dispatched <paramref name="parse"/> using
    /// <paramref name="localHashSalt"/> from <see cref="CssComponentHash.Resolve"/>. The generator
    /// shares its container parse with this compiler, so each component is parsed once.
    /// </summary>
    /// <param name="parse">The dispatched <c>.viu</c> or <c>.vue</c> parse; its style source results are read.</param>
    /// <param name="localHashSalt">The deterministic component-local salt for module and binding names.</param>
    /// <param name="cancellationToken">Cancels the compilation.</param>
    /// <returns>The component's style compilation.</returns>
    public static SingleFileComponentStyleCompilation Compile(
        AggregateSyntaxParserResult<SingleFileComponentBlock> parse,
        string localHashSalt,
        CancellationToken cancellationToken = default)
    {
        if (parse is null)
        {
            throw new ArgumentNullException(nameof(parse));
        }

        StringBuilder? styles = null;
        List<SingleFileComponentStyleModuleClass>? moduleClasses = null;
        List<SingleFileComponentStyleVariableBinding>? variableBindings = null;
        List<SingleFileComponentStyleDiagnostic>? diagnostics = null;

        foreach (var sourceResult in parse.SourceResults)
        {
            if (!SingleFileComponentParserFactory.IsStyleBlock(sourceResult.Source) ||
                sourceResult.Node is not SingleFileComponentStyleBlock styleBlock)
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();

            string css;
            if (sourceResult.Result.Nodes.Count > 0 && sourceResult.Result.Nodes[0] is CssStylesheetNode parsed)
            {
                var stylesheet = parsed;
                var rewritten = false;

                // `module`: rename local class selectors and record original -> hashed for the accessor.
                if (styleBlock.IsModule)
                {
                    var moduleResult = CssModuleRewriter.Rewrite(stylesheet, localHashSalt);
                    stylesheet = moduleResult.Stylesheet;
                    rewritten = rewritten || moduleResult.Classes.Count > 0;
                    var accessor = ModuleAccessorName(styleBlock.ModuleName);
                    foreach (var pair in moduleResult.Classes)
                    {
                        (moduleClasses ??= new List<SingleFileComponentStyleModuleClass>())
                            .Add(new SingleFileComponentStyleModuleClass(accessor, pair.Key, pair.Value, styleBlock.ModuleName));
                    }
                }

                // `v-bind()`: rewrite each usage to a custom property and record the (hash, expression)
                // binding. The content guard skips the rewrite for the common no-binding block.
                if (styleBlock.Content.IndexOf("v-bind", StringComparison.Ordinal) >= 0)
                {
                    var bindingResult = CssBindingRewriter.Rewrite(stylesheet, localHashSalt);
                    stylesheet = bindingResult.Stylesheet;
                    rewritten = rewritten || bindingResult.Bindings.Count > 0;
                    var blockContentStart = sourceResult.Node.ContentLocation.Start;
                    foreach (var binding in bindingResult.Bindings)
                    {
                        (variableBindings ??= new List<SingleFileComponentStyleVariableBinding>())
                            .Add(new SingleFileComponentStyleVariableBinding(binding, blockContentStart));
                    }

                    foreach (var diagnostic in bindingResult.Diagnostics)
                    {
                        (diagnostics ??= new List<SingleFileComponentStyleDiagnostic>())
                            .Add(new SingleFileComponentStyleDiagnostic(diagnostic, blockContentStart));
                    }
                }

                if (rewritten)
                {
                    // A rewritten block is serialized canonically (its class names / values changed,
                    // so the raw content no longer matches).
                    css = CssStylesheetWriter.Write(stylesheet);
                }
                else
                {
                    // An untouched ordinary block passes through verbatim (issue acceptance criterion).
                    css = styleBlock.Content;
                }
            }
            else
            {
                css = styleBlock.Content;
            }

            styles ??= new StringBuilder();
            styles.Append(css);
            if (css.Length > 0 && css[css.Length - 1] != '\n')
            {
                styles.Append('\n');
            }
        }

        if (styles is null)
        {
            return SingleFileComponentStyleCompilation.Empty;
        }

        return new SingleFileComponentStyleCompilation(
            styles.ToString(),
            (IReadOnlyList<SingleFileComponentStyleModuleClass>?)moduleClasses ?? Array.Empty<SingleFileComponentStyleModuleClass>(),
            (IReadOnlyList<SingleFileComponentStyleVariableBinding>?)variableBindings ?? Array.Empty<SingleFileComponentStyleVariableBinding>(),
            (IReadOnlyList<SingleFileComponentStyleDiagnostic>?)diagnostics ?? Array.Empty<SingleFileComponentStyleDiagnostic>());
    }

    // The generated accessor class name for a `module` option: the default (valueless `module`) maps to
    // `Style`, because the `$style` name a template writes has no legal C# spelling; `module="name"` maps
    // to the pascal-cased name.
    private static string ModuleAccessorName(string? moduleName)
        => string.IsNullOrEmpty(moduleName) ? "Style" : PascalCase(moduleName!);

    // Pascal-cases an authored identifier for use as a C# type/member name: word boundaries at '-'/'_'/' '
    // start a new capitalized word, a leading digit is prefixed with '_', and non-identifier characters are
    // dropped. Deterministic so the emitted accessor is stable.
    private static string PascalCase(string value)
    {
        var builder = new StringBuilder(value.Length);
        var capitalizeNext = true;
        foreach (var character in value)
        {
            if (character == '-' || character == '_' || character == ' ')
            {
                capitalizeNext = true;
                continue;
            }

            if (!char.IsLetterOrDigit(character))
            {
                continue;
            }

            if (builder.Length == 0 && char.IsDigit(character))
            {
                builder.Append('_');
            }

            builder.Append(capitalizeNext ? char.ToUpperInvariant(character) : character);
            capitalizeNext = false;
        }

        return builder.Length == 0 ? "Style" : builder.ToString();
    }
}
