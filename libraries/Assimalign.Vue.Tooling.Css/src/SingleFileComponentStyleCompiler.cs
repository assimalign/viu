using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using Assimalign.Vue.Syntax.Css;
using Assimalign.Vue.Syntax.SingleFileComponent;

namespace Assimalign.Vue.Tooling.Css;

/// <summary>
/// Compiles a <c>.viu</c> file's <c>@style</c> blocks into the component's extracted CSS — the shared,
/// deterministic core reused by <b>both</b> build-time hosts ([V01.01.12.12]). Each block is run through the
/// CSS-Modules class rename (<c>module</c> blocks, <see cref="CssModuleRewriter"/>), the <c>v-bind()</c>
/// custom-property rewrite (<see cref="CssBindingRewriter"/>), and then serialized — <c>scoped</c> blocks via
/// <see cref="CssScopedRewriter"/> with the component's stable scope id, rewritten non-scoped blocks via
/// <see cref="CssStylesheetWriter"/>, and untouched non-scoped blocks verbatim — concatenated in source order.
/// <para>
/// This is the exact logic that produced the generator's <c>ExtractedStyles</c> constant when it lived inside
/// <c>Assimalign.Vue.Syntax.Generators</c>; it was lifted here unchanged so the generator and the
/// <c>VuecsBundleCss</c> MSBuild task run <em>one</em> deterministic implementation over the same inputs. That
/// single-path design is what makes the emitted constant and the physical bundle byte-identical — there is no
/// second, divergent generation path (see <c>docs/UTILITY-CSS-DESIGN.md</c> §2.4). No I/O, no reflection, no
/// dynamic codegen; recoverable (malformed <c>v-bind()</c> surfaces as a diagnostic, never throws) — the only
/// expected exception is <see cref="OperationCanceledException"/>.
/// </para>
/// </summary>
public static class SingleFileComponentStyleCompiler
{
    // The scope-id prefix ([V01.01.06.04] StyleScopeId), stripped to recover the short salt the module/v-bind
    // hashes use ([V01.01.06.06]).
    private const string ScopeIdPrefix = "data-v-";

    /// <summary>
    /// Parses <paramref name="viuText"/> with <paramref name="parser"/>, resolves the scope id from the path,
    /// and compiles the component's <c>@style</c> blocks. The convenience entry point for the
    /// <c>VuecsBundleCss</c> task, which starts from raw file text; the generator uses
    /// <see cref="Compile(SingleFileComponentSyntaxParserResult, string, CancellationToken)"/> directly
    /// because it already holds the shared parse. Both routes run the identical compilation.
    /// </summary>
    /// <param name="parser">The composed parser from <see cref="SingleFileComponentParserFactory.Create"/>.</param>
    /// <param name="viuText">The <c>.viu</c> file's full text.</param>
    /// <param name="filePath">The <c>.viu</c> file path (drives the scope-id hash).</param>
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
        var scopeId = StyleScopeId.Resolve(filePath, projectDirectory);
        return Compile(parse, scopeId, cancellationToken);
    }

    /// <summary>
    /// Compiles the <c>@style</c> blocks in an already-dispatched <paramref name="parse"/> using
    /// <paramref name="componentScopeId"/> (the component's <c>data-v-&lt;hash&gt;</c>, from
    /// <see cref="StyleScopeId.Resolve"/>). The generator host calls this directly with the parse and scope id
    /// it already computed for template compilation, so the <c>.viu</c> is parsed once.
    /// </summary>
    /// <param name="parse">The dispatched <c>.viu</c> parse (its <c>@style</c> source results are read).</param>
    /// <param name="componentScopeId">The component's <c>data-v-&lt;hash&gt;</c> scope id.</param>
    /// <param name="cancellationToken">Cancels the compilation.</param>
    /// <returns>The component's style compilation.</returns>
    public static SingleFileComponentStyleCompilation Compile(
        SingleFileComponentSyntaxParserResult parse,
        string componentScopeId,
        CancellationToken cancellationToken = default)
    {
        if (parse is null)
        {
            throw new ArgumentNullException(nameof(parse));
        }

        string? scopeId = null;
        StringBuilder? styles = null;
        List<SingleFileComponentStyleModuleClass>? moduleClasses = null;
        List<CssVariableBinding>? variableBindings = null;
        List<SingleFileComponentStyleDiagnostic>? diagnostics = null;

        // The module/v-bind hashes are salted by the component's short scope id (the path hash without the
        // `data-v-` prefix), which is always available — so a `module`/`v-bind` block is component-scoped even
        // when it is not `scoped` ([V01.01.06.06]).
        var localHashSalt = ShortScopeId(componentScopeId);

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
                            .Add(new SingleFileComponentStyleModuleClass(accessor, pair.Key, pair.Value));
                    }
                }

                // `v-bind()`: rewrite each usage to a custom property and record the (hash, expression)
                // binding. The content guard skips the rewrite for the common no-binding block.
                if (styleBlock.Content.IndexOf("v-bind", StringComparison.Ordinal) >= 0)
                {
                    var bindingResult = CssBindingRewriter.Rewrite(stylesheet, localHashSalt);
                    stylesheet = bindingResult.Stylesheet;
                    rewritten = rewritten || bindingResult.Bindings.Count > 0;
                    foreach (var binding in bindingResult.Bindings)
                    {
                        (variableBindings ??= new List<CssVariableBinding>()).Add(binding);
                    }

                    var blockContentStart = sourceResult.Node.ContentLocation.Start;
                    foreach (var diagnostic in bindingResult.Diagnostics)
                    {
                        (diagnostics ??= new List<SingleFileComponentStyleDiagnostic>())
                            .Add(new SingleFileComponentStyleDiagnostic(diagnostic, blockContentStart));
                    }
                }

                if (styleBlock.Scoped)
                {
                    // A scoped block is serialized with the component's stable scope id (module/v-bind rewrites
                    // already updated the tree the scoped serializer reads).
                    scopeId ??= componentScopeId;
                    css = CssScopedRewriter.Rewrite(stylesheet, scopeId);
                }
                else if (rewritten)
                {
                    // A rewritten non-scoped block is serialized canonically (its class names / values changed,
                    // so the raw content no longer matches).
                    css = CssStylesheetWriter.Write(stylesheet);
                }
                else
                {
                    // An untouched non-scoped block passes through verbatim (issue acceptance criterion).
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
            scopeId,
            styles.ToString(),
            (IReadOnlyList<SingleFileComponentStyleModuleClass>?)moduleClasses ?? Array.Empty<SingleFileComponentStyleModuleClass>(),
            (IReadOnlyList<CssVariableBinding>?)variableBindings ?? Array.Empty<CssVariableBinding>(),
            (IReadOnlyList<SingleFileComponentStyleDiagnostic>?)diagnostics ?? Array.Empty<SingleFileComponentStyleDiagnostic>());
    }

    // The component's short scope id — the `data-v-` scope id with its prefix stripped — used to salt the
    // module/v-bind hashes so they are component-scoped and deterministic ([V01.01.06.06]).
    private static string ShortScopeId(string scopeId)
        => scopeId.StartsWith(ScopeIdPrefix, StringComparison.Ordinal)
            ? scopeId.Substring(ScopeIdPrefix.Length)
            : scopeId;

    // The generated accessor class name for a `module` option: default (valueless `module`) maps to `Style` —
    // the C# analogue of Vue's `$style`, which has no legal C# spelling — and `module="name"` maps to the
    // pascal-cased name.
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
