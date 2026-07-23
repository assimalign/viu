using System.Collections.Generic;
using System.Linq;

using Assimalign.Viu.Shared;

using Shouldly;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// Shared helpers for the transform test corpus: parse a template in DOM mode and run the transform pipeline,
/// then navigate the resulting code-generation IR. Mirrors the <c>transformWithXxx</c> helpers in vuejs/core's
/// <c>packages/compiler-core/__tests__/transforms/*.spec.ts</c>.
/// </summary>
internal static class TransformTestHelpers
{
    /// <summary>Parses <paramref name="source"/> (DOM mode) and transforms it with the default DOM transforms.</summary>
    public static TransformResult Transform(string source, TransformOptions? options = null)
    {
        var root = TemplateParser.Parse(source, ParserOptions.CreateHtml());
        return Transformer.Transform(root, options ?? TransformOptions.CreateDom());
    }

    /// <summary>Parses and transforms, collecting reported diagnostics.</summary>
    public static TransformResult Transform(string source, out List<CompilerError> errors, TransformOptions? options = null)
    {
        var collected = new List<CompilerError>();
        options ??= TransformOptions.CreateDom();
        options.OnError = collected.Add;
        var result = Transform(source, options);
        errors = collected;
        return result;
    }

    /// <summary>The single top-level transformed child.</summary>
    public static TemplateChildNode SingleChild(this TransformResult result)
        => result.Children.ShouldHaveSingleItem();

    /// <summary>Whether a helper with the given runtime name was registered.</summary>
    public static bool UsesHelper(this TransformResult result, string name)
        => result.Helpers.Any(helper => helper.Name == name);

    /// <summary>Asserts a helper with the given runtime name was registered.</summary>
    public static void ShouldUseHelper(this TransformResult result, string name)
        => result.UsesHelper(name).ShouldBeTrue($"expected helper '{name}' to be registered");

    /// <summary>The code-generation node of the single top-level child.</summary>
    public static TemplateSyntaxNode RootCodegen(this TransformResult result)
        => result.CodegenNode.ShouldNotBeNull();

    /// <summary>Asserts the property list contains a property whose static key equals <paramref name="key"/> and returns it.</summary>
    public static Property Property(this ObjectExpression obj, string key)
        => obj.Properties.FirstOrDefault(p => p.Key is SimpleExpressionNode { IsStatic: true } k && k.Content == key)
            .ShouldNotBeNull($"expected property '{key}'");

    /// <summary>The content of a static simple-expression node.</summary>
    public static string StaticContent(this TemplateSyntaxNode node)
        => node.ShouldBeOfType<SimpleExpressionNode>().Content;

    /// <summary>Asserts a patch flag is present on a vnode call.</summary>
    public static void ShouldHavePatchFlag(this VNodeCall call, PatchFlags flag)
    {
        call.PatchFlag.ShouldNotBeNull();
        (call.PatchFlag!.Value & flag).ShouldBe(flag);
    }
}
