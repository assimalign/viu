using System;
using System.Collections.Generic;
using System.Text;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// Compiles a single, standalone C# expression through the template compiler's binding-metadata rewriting —
/// the same identifier classification and <c>Ref&lt;T&gt;</c> unwrapping ([V01.01.05.04]) a template expression
/// receives, but for an expression that lives outside a template. The composition-root generator uses it for
/// <c>v-bind()</c> CSS expressions ([V01.01.06.06.01]): routing them through this compiler makes
/// <c>v-bind(count)</c> unwrap a script <c>Reference&lt;T&gt;</c> to <c>count.Value</c> automatically, matching
/// upstream's cssVars ergonomics, instead of forcing the author to write <c>v-bind(count.Value)</c>.
/// </summary>
/// <remarks>
/// The <c>v-bind()</c> getter runs as an <em>instance member</em> of the component partial class, so this compiles
/// in <see cref="BindingRewriteMode.InstanceMember"/>: bindings read through the implicit <c>this</c> (no
/// <c>_ctx.</c>), a definite reference unwraps to <c>name.Value</c>, and every other classification reads bare —
/// the getter needs no runtime-helper import. A malformed expression is reported through
/// <see cref="ExpressionCompileResult.Diagnostics"/> (never thrown) with its span remapped back onto the supplied
/// <see cref="SourceLocation"/>, so the composition root lands it on the exact <c>.viu</c> coordinate.
/// </remarks>
public static class TemplateExpressionCompiler
{
    /// <summary>
    /// Compiles <paramref name="expression"/> for an instance-member context, classifying its identifiers against
    /// <paramref name="bindingMetadata"/> and unwrapping definite references.
    /// </summary>
    /// <param name="expression">The raw C# expression text.</param>
    /// <param name="bindingMetadata">The component binding classifications to resolve identifiers against.</param>
    /// <param name="location">
    /// The expression's source location — diagnostics are remapped onto it (its <see cref="SourceLocation.Start"/>
    /// anchors the reported span), so pass the expression's position in the originating file/block.
    /// </param>
    /// <returns>The rewritten code and any diagnostics.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static ExpressionCompileResult CompileInstanceExpression(
        string expression,
        BindingMetadata bindingMetadata,
        SourceLocation location)
    {
        if (expression is null)
        {
            throw new ArgumentNullException(nameof(expression));
        }

        if (bindingMetadata is null)
        {
            throw new ArgumentNullException(nameof(bindingMetadata));
        }

        if (location is null)
        {
            throw new ArgumentNullException(nameof(location));
        }

        var diagnostics = new List<CompilerError>();
        var options = new TransformOptions
        {
            PrefixIdentifiers = true,
            BindingMetadata = bindingMetadata,
            BindingRewriteMode = BindingRewriteMode.InstanceMember,
            OnError = diagnostics.Add,
        };

        // A minimal transform context: the expression compiler needs no template tree, only the binding
        // classification, the rewrite mode, and the error sink. Empty transforms keep it inert.
        var root = new RootNode
        {
            Children = new SyntaxList<TemplateChildNode>(Array.Empty<TemplateChildNode>()),
            Source = string.Empty,
            Location = Ir.LocationStub,
        };
        var context = new TransformContext(
            root,
            Array.Empty<NodeTransform>(),
            new Dictionary<string, DirectiveTransform>(),
            options);

        var node = new SimpleExpressionNode { Content = expression, IsStatic = false, Location = location };
        var processed = ExpressionProcessor.ProcessExpression(node, context);
        return new ExpressionCompileResult(Flatten(processed), diagnostics);
    }

    // Serializes a rewritten expression node back to C# text: a simple expression is its content, a compound is
    // the concatenation of its parts (raw strings and nested simple/compound expressions).
    private static string Flatten(object? part)
    {
        switch (part)
        {
            case null:
                return string.Empty;
            case string text:
                return text;
            case SimpleExpressionNode simple:
                return simple.Content;
            case CompoundExpressionNode compound:
            {
                var builder = new StringBuilder();
                foreach (var child in compound.Parts)
                {
                    builder.Append(Flatten(child));
                }

                return builder.ToString();
            }

            default:
                return part.ToString() ?? string.Empty;
        }
    }
}
