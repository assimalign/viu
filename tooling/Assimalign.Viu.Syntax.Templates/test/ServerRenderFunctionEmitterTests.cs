using System;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Shouldly;
using Xunit;

using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// Pins [V01.01.07.02]'s build-time server-markup target independently of the interactive
/// virtual-node target. Runtime byte equality is covered in ServerRenderer's differential suite.
/// </summary>
public sealed class ServerRenderFunctionEmitterTests
{
    [Fact]
    public void ServerMarkup_StaticWithInterpolation_EmitsOnlyWritesAndSerializationHelpers()
    {
        RenderFunctionEmitterResult emitted = Emit(
            "<section class=\"hero\"><h1>{{ title }}</h1><p>ready</p></section>");

        emitted.Code.ShouldContain("state.Push(\"<section");
        emitted.Code.ShouldContain("ServerRender.SsrInterpolate(component.title)");
        emitted.Code.ShouldContain("ready</p></section>");
        emitted.Code.ShouldNotContain("new global::Assimalign.Viu.Components.");
        emitted.Code.ShouldNotContain("VirtualNode");
    }

    [Fact]
    public void ServerMarkup_ModelControlsAndShow_EmitSerializedState()
    {
        string input = Emit("<input v-model=\"name\">").Code;
        string checkbox = Emit("<input type=\"checkbox\" value=\"yes\" v-model=\"choices\">").Code;
        string textArea = Emit("<textarea v-model=\"notes\">ignored</textarea>").Code;
        string select = Emit(
            "<select v-model=\"choice\"><option value=\"a\">A</option><option :value=\"other\">B</option></select>").Code;
        string implicitOptionValue = Emit(
            "<select v-model=\"choice\"><option>{{ choice }}</option></select>").Code;
        string show = Emit("<div :style=\"styles\" v-show=\"visible\">shown</div>").Code;

        input.ShouldContain("SsrRenderDynamicAttribute(\"value\", component.name, \"input\")");
        checkbox.ShouldContain("SsrRenderDynamicAttribute(\"value\", \"yes\", \"input\")");
        checkbox.ShouldContain("IsModelValueSelected(component.choices, \"yes\", true)");
        checkbox.ShouldContain("SsrRenderDynamicAttribute(\"checked\"");
        textArea.ShouldContain("ServerRender.SsrInterpolate(component.notes)");
        textArea.ShouldNotContain("SsrRenderDynamicAttribute(\"value\"");
        select.OccurrencesOf("IsModelValueSelected(component.choice").ShouldBe(2);
        select.OccurrencesOf("SsrRenderDynamicAttribute(\"selected\"").ShouldBe(2);
        implicitOptionValue.ShouldContain(
            "IsModelValueSelected(component.choice, " +
            "global::Assimalign.Viu.DisplayStringFormatter.ToDisplayString(component.choice), false)");
        show.ShouldContain("ServerRender.SsrRenderStyle(component.styles)");
        show.ShouldContain("if (!global::Assimalign.Viu.ServerRenderer.ServerRender.IsTruthy(component.visible))");
        show.ShouldContain("state.Push(\"display:none;\")");
    }

    [Fact]
    public void ServerMarkup_ScopedStyleIdentifier_IsWrittenOnEveryNativeElement()
    {
        string code = Emit(
            "<main><h1>heading</h1><p>body</p></main>",
            scopeId: "data-v-c0ffee00").Code;

        code.OccurrencesOf("SsrRenderDynamicAttribute(\"data-v-c0ffee00\"")
            .ShouldBe(3);
    }

    [Fact]
    public void VirtualNodeTree_ScopedStyleIdentifier_IsBoundOnEveryNativeElement()
    {
        RootNode root = TemplateParser.Parse(
            "<main><h1>heading</h1><p>body</p></main>",
            ParserOptions.CreateHtml());
        TransformOptions options = TransformOptions.CreateDom();
        options.PrefixIdentifiers = true;
        options.BindingMetadata = BindingMetadata.Empty;
        options.ScopeId = "data-v-c0ffee00";

        string code = RenderFunctionEmitter.Emit(Transformer.Transform(root, options)).Code;

        code.OccurrencesOf("new global::Assimalign.Viu.Components.QualifiedName(\"data-v-c0ffee00\")")
            .ShouldBe(3);
    }

    [Fact]
    public void ServerMarkup_DynamicComponent_UsesSubtreeLocalVirtualNodeFallback()
    {
        string code = Emit("<component :is=\"viewName\"></component>").Code;

        code.ShouldContain("VirtualNode? RenderFallback");
        code.ShouldContain("DynamicComponents.Create(");
        code.ShouldContain("ServerRender.SsrRenderComponentAsync(state, RenderFallback");
    }

    [Fact]
    public void ServerMarkup_DynamicAttributeSubtree_UsesVirtualNodeFallback()
    {
        string code = Emit(
            "<div v-bind=\"attributes\"><span>{{ body }}</span></div>").Code;

        code.ShouldContain("VirtualNode? RenderFallback");
        code.ShouldContain("ElementBinding.Attribute");
        code.ShouldContain("ServerRender.SsrRenderComponentAsync(state, RenderFallback");
    }

    [Fact]
    public void ServerMarkup_PropertyBinding_UsesMappedSubtreeLocalVirtualNodeFallback()
    {
        RenderFunctionEmitterResult emitted = Emit("<input :value.prop=\"body\">");

        emitted.Code.ShouldContain("VirtualNode? RenderFallback");
        emitted.Code.ShouldContain("ElementBinding.Property(");
        emitted.Code.ShouldContain("component.body");
        emitted.Code.ShouldContain("ServerRender.SsrRenderComponentAsync(state, RenderFallback");
        emitted.SourceMappings.Any(mapping => mapping.TemplateLocation.Source == "body")
            .ShouldBeTrue();
    }

    [Fact]
    public void ServerMarkup_FragmentTemplate_PreservesHydrationMarkers()
    {
        string code = Emit(
            "<template v-if=\"!visible\"><span>one</span><span>two</span></template>").Code;

        code.ShouldContain("HydrationMarkers.FragmentStart");
        code.ShouldContain("HydrationMarkers.FragmentEnd");
    }

    [Fact]
    public void ServerMarkup_BuiltIns_UseMarkerPreservingServerHelpers()
    {
        string teleport = Emit("<Teleport to=\"#target\"><span>away</span></Teleport>").Code;
        string suspense = Emit(
            "<Suspense><template #default><strong>ready</strong></template>" +
            "<template #fallback><em>wait</em></template></Suspense>").Code;
        string transition = Emit("<Transition><div>shown</div></Transition>").Code;

        teleport.ShouldContain("ServerRender.SsrRenderTeleportAsync(");
        teleport.ShouldNotContain("teleport start");
        suspense.ShouldContain("ServerRender.SsrRenderSuspenseAsync(");
        suspense.ShouldContain("<strong>ready</strong>");
        suspense.ShouldNotContain("<em>wait</em>");
        transition.ShouldContain("state.Push(\"<div>shown</div>\")");
        transition.ShouldNotContain("TransitionNode");
    }

    [Theory]
    [InlineData("<div :id=\"identifier\">{{ message }}</div>")]
    [InlineData("<div v-if=\"visible\">yes</div><p v-else>no</p>")]
    [InlineData("<ul><li v-for=\"item in items\">{{ item }}</li></ul>")]
    [InlineData("<component :is=\"viewName\"><span>{{ body }}</span></component>")]
    [InlineData("<div v-bind=\"attributes\"><span>{{ body }}</span></div>")]
    [InlineData("<Teleport to=\"body\"><div>away</div></Teleport>")]
    [InlineData("<input v-model=\"name\">")]
    [InlineData("<select v-model=\"choice\"><option>{{ choice }}</option></select>")]
    public void ServerMarkup_EmittedBody_ParsesAsValidCSharp(string source)
    {
        string code = Emit(source).Code;
        string unit =
            "internal sealed class RenderProbe {\n" +
            "    private static async global::System.Threading.Tasks.Task Render(" +
            "global::Assimalign.Viu.ServerRenderer.SsrRenderState state, RenderProbe component, " +
            "global::Assimalign.Viu.Components.ComponentRenderFrame frame, " +
            "global::Assimalign.Viu.IComponentRenderScope? parent) {\n" +
            code +
            "    }\n" +
            "}";

        SyntaxTree tree = CSharpSyntaxTree.ParseText(
            unit,
            new CSharpParseOptions(LanguageVersion.Preview));
        tree.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == RoslynDiagnosticSeverity.Error)
            .ShouldBeEmpty(customMessage: code);
    }

    [Fact]
    public void ServerMarkup_ProfileRequiresServerTransform()
    {
        RootNode root = TemplateParser.Parse("<div>text</div>", ParserOptions.CreateHtml());
        TransformResult transformed = Transformer.Transform(root, TransformOptions.CreateDom());

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
            RenderFunctionEmitter.Emit(
                transformed,
                new RenderFunctionEmitterOptions
                {
                    TargetProfile = RenderFunctionTargetProfile.ServerMarkup,
                }));

        exception.Message.ShouldContain("IsServerRendering");
    }

    private static RenderFunctionEmitterResult Emit(string source, string? scopeId = null)
    {
        RootNode root = TemplateParser.Parse(source, ParserOptions.CreateHtml());
        TransformOptions transformOptions = TransformOptions.CreateDom();
        transformOptions.PrefixIdentifiers = true;
        transformOptions.BindingMetadata = BindingMetadata.Empty;
        transformOptions.IsServerRendering = true;
        transformOptions.ScopeId = scopeId;
        TransformResult transformed = Transformer.Transform(root, transformOptions);
        return RenderFunctionEmitter.Emit(
            transformed,
            new RenderFunctionEmitterOptions
            {
                TargetProfile = RenderFunctionTargetProfile.ServerMarkup,
            });
    }
}

internal static class ServerRenderCodeStringExtensions
{
    internal static int OccurrencesOf(this string value, string fragment)
    {
        int count = 0;
        int offset = 0;
        while ((offset = value.IndexOf(fragment, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += fragment.Length;
        }

        return count;
    }
}
