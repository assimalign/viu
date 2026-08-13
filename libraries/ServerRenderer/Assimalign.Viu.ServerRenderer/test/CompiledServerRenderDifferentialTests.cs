using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.State;
using Assimalign.Viu.Syntax.Templates;

using ComponentCommentNode = Assimalign.Viu.Components.CommentNode;
using ComponentElementNode = Assimalign.Viu.Components.ElementNode;
using ComponentFragmentNode = Assimalign.Viu.Components.FragmentNode;
using ComponentTextNode = Assimalign.Viu.Components.TextNode;
using ComponentVirtualNode = Assimalign.Viu.Components.VirtualNode;
using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

namespace Assimalign.Viu.ServerRenderer.Tests;

/// <summary>
/// Executes [V01.01.07.02]'s generated server body and the existing virtual-tree serializer over
/// equivalent fixtures. Every comparison is byte equality, including hydration markers.
/// </summary>
public sealed class CompiledServerRenderDifferentialTests
{
    /// <summary>Gets the templates exercised by the byte-equality differential test.</summary>
    public static IEnumerable<object[]> FixtureSource =>
    [
        ["<section id=\"root\"><h1>{{ title }}</h1><p :class=\"classes\" :style=\"styles\" :title=\"title\">{{ body }}</p><!--safe--></section>", ""],
        ["<span>{{ title }}</span><input disabled :title=\"body\">", ""],
        ["<textarea :value=\"body\">ignored</textarea>", ""],
        ["<div v-if=\"visible\">yes</div>", ""],
        ["<ul><li v-for=\"item in items\">{{ item }}</li></ul>", ""],
        ["<input v-model=\"modelText\">", ""],
        ["<input type=\"checkbox\" value=\"yes\" v-model=\"checkboxModel\">", ""],
        ["<textarea v-model=\"body\">ignored</textarea>", ""],
        ["<select v-model=\"choice\"><option value=\"one\">One</option><option :value=\"second\">Two</option></select>", ""],
        ["<select v-model=\"choice\"><option>{{ choice }}</option></select>", ""],
        ["<div :style=\"styles\" v-show=\"visible\">hidden</div>", ""],
        ["<main><h1>heading</h1><p>body</p></main>", "data-v-c0ffee00"],
        ["<template v-if=\"!visible\"><span>one</span><span>two</span></template>", ""],
        ["<component :is=\"viewName\"><span>{{ body }}</span></component>", ""],
        ["<div v-bind=\"attributes\"><span>{{ body }}</span></div>", "data-v-fallback00"],
        ["<input :value.prop=\"body\">", ""],
    ];

    [Theory]
    [MemberData(nameof(FixtureSource))]
    public async Task CompiledMarkup_EquivalentVirtualTree_IsByteIdentical(
        string source,
        string scopeId)
    {
        MethodInfo renderMethod = CompileServerRender(
            source,
            out object component,
            string.IsNullOrEmpty(scopeId) ? null : scopeId);
        ComponentVirtualNode referenceTree = CreateReferenceTree(source);
        ComponentFactory components = new();
        ServerRenderApplication compiledApplication = new(new ComponentTextNode("unused"), components);

        string compiled = await ServerRender.RenderCompiledToStringAsync(
            compiledApplication,
            state => (Task)renderMethod.Invoke(
                null,
                [state, component, new ComponentRenderFrame(), null])!);
        string traversed = await ServerRender.RenderToStringAsync(referenceTree);

        compiled.ShouldBe(traversed);
    }

    [Fact]
    public async Task CompiledMarkup_Teleport_UsesUnchangedMarkerAndTargetProtocol()
    {
        const string source = "<Teleport to=\"#target\"><span>{{ body }}</span></Teleport>";
        MethodInfo renderMethod = CompileServerRender(source, out object component);
        ComponentFactory components = new();
        SsrContext context = new();

        string compiled = await ServerRender.RenderCompiledToStringAsync(
            new ServerRenderApplication(new ComponentTextNode("unused"), components),
            state => (Task)renderMethod.Invoke(
                null,
                [state, component, new ComponentRenderFrame(), null])!,
            context);

        compiled.ShouldBe(HydrationMarkers.TeleportStart + HydrationMarkers.TeleportEnd);
        context.Teleports["#target"].ShouldBe(
            "<span>body &amp; text</span>" + HydrationMarkers.TeleportAnchor);
    }

    [Fact]
    public async Task CompiledMarkup_StreamPath_FlushesDirectWrites()
    {
        ComponentFactory components = new();
        using StringWriter writer = new();

        await ServerRender.RenderCompiledToStreamAsync(
            new ServerRenderApplication(new ComponentTextNode("unused"), components),
            state =>
            {
                state.Push("<p>streamed</p>");
                return Task.CompletedTask;
            },
            writer);

        writer.ToString().ShouldBe("<p>streamed</p>");
    }

    [Fact]
    public async Task CompiledMarkup_PreCancelledRequest_DoesNotInvokeGeneratedBody()
    {
        ComponentFactory components = new();
        using CancellationTokenSource cancellationSource = new();
        cancellationSource.Cancel();
        bool invoked = false;

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await ServerRender.RenderCompiledToStringAsync(
                new ServerRenderApplication(new ComponentTextNode("unused"), components),
                _ =>
                {
                    invoked = true;
                    return Task.CompletedTask;
                },
                cancellationToken: cancellationSource.Token));

        invoked.ShouldBeFalse();
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "This test deliberately compiles and loads ephemeral generated code; shipping code does not.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2075",
        Justification = "The test-owned generated type has the public constructor and method declared below.")]
    private static MethodInfo CompileServerRender(
        string source,
        out object component,
        string? scopeId = null)
    {
        RootNode root = TemplateParser.Parse(source, ParserOptions.CreateHtml());
        TransformOptions transformOptions = TransformOptions.CreateDom();
        transformOptions.PrefixIdentifiers = true;
        transformOptions.BindingMetadata = BindingMetadata.Empty;
        transformOptions.IsServerRendering = true;
        transformOptions.ScopeId = scopeId;
        TransformResult transformed = Transformer.Transform(root, transformOptions);
        RenderFunctionEmitterResult emitted = RenderFunctionEmitter.Emit(
            transformed,
            new RenderFunctionEmitterOptions
            {
                IndentLevel = 2,
                TargetProfile = RenderFunctionTargetProfile.ServerMarkup,
            });

        string generatedSource =
            "#nullable enable\n" +
            "public sealed class CompiledRenderProbe\n" +
            "{\n" +
            "    public string title = \"<heading>\";\n" +
            "    public string body = \"body & text\";\n" +
            "    public object? classes = new object?[] { \"hero\", " +
            "new global::System.Collections.Generic.Dictionary<string, object?> { [\"active\"] = true } };\n" +
            "    public object? styles = new global::System.Collections.Generic.Dictionary<string, object?> " +
            "{ [\"fontSize\"] = \"12px\", [\"color\"] = \"red\" };\n" +
            "    public bool visible = false;\n" +
            "    public string[] items = new string[] { \"one\", \"<two>\" };\n" +
            "    public string modelText = \"typed & ready\";\n" +
            "    public object? checkboxModel = new string[] { \"yes\" };\n" +
            "    public string choice = \"two\";\n" +
            "    public string second = \"two\";\n" +
            "    public object? viewName = \"aside\";\n" +
            "    public object? attributes = new global::System.Collections.Generic.Dictionary<string, object?> " +
            "{ [\"title\"] = \"<fallback>\" };\n" +
            "    public static async global::System.Threading.Tasks.Task RenderAsync(\n" +
            "        global::Assimalign.Viu.ServerRenderer.SsrRenderState state,\n" +
            "        CompiledRenderProbe component,\n" +
            "        global::Assimalign.Viu.Components.ComponentRenderFrame frame,\n" +
            "        global::Assimalign.Viu.IComponentRenderScope? parent)\n" +
            "    {\n" +
            emitted.Code +
            "        await global::System.Threading.Tasks.Task.CompletedTask;\n" +
            "    }\n" +
            "}\n";

        CSharpCompilation compilation = CSharpCompilation.Create(
            "CompiledServerRenderDifferential_" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(
                generatedSource,
                new CSharpParseOptions(LanguageVersion.Preview))],
            RuntimeReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        using MemoryStream assemblyStream = new();
        EmitResult result = compilation.Emit(assemblyStream);
        result.Diagnostics
            .Where(diagnostic => diagnostic.Severity == RoslynDiagnosticSeverity.Error)
            .ShouldBeEmpty(customMessage: generatedSource);

        assemblyStream.Position = 0;
        Assembly assembly = AssemblyLoadContext.Default.LoadFromStream(assemblyStream);
        Type probeType = assembly.GetType("CompiledRenderProbe", throwOnError: true)!;
        component = probeType.GetConstructor(Type.EmptyTypes)!.Invoke(null);
        return probeType.GetMethod("RenderAsync", BindingFlags.Public | BindingFlags.Static)!;
    }

    [UnconditionalSuppressMessage(
        "SingleFile",
        "IL3000",
        Justification = "The non-published test runner supplies physical reference assemblies.")]
    private static IEnumerable<MetadataReference> RuntimeReferences()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? platformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (!string.IsNullOrEmpty(platformAssemblies))
        {
            foreach (string path in platformAssemblies!.Split(Path.PathSeparator))
            {
                if (!string.Equals(
                    Path.GetFileNameWithoutExtension(path),
                    "Assimalign.Viu.Syntax.Templates",
                    StringComparison.OrdinalIgnoreCase))
                {
                    paths.Add(path);
                }
            }
        }

        paths.Add(typeof(ComponentVirtualNode).Assembly.Location);
        paths.Add(typeof(IApplicationContext).Assembly.Location);
        paths.Add(typeof(ServerRender).Assembly.Location);
        paths.Add(typeof(StateStoreRegistry).Assembly.Location);
        return paths.Select(static path =>
            (MetadataReference)MetadataReference.CreateFromFile(path));
    }

    private static ComponentVirtualNode CreateReferenceTree(string source)
    {
        object? classes = new object?[]
        {
            "hero",
            new Dictionary<string, object?> { ["active"] = true },
        };
        object? styles = new Dictionary<string, object?>
        {
            ["fontSize"] = "12px",
            ["color"] = "red",
        };

        return source switch
        {
            "<section id=\"root\"><h1>{{ title }}</h1><p :class=\"classes\" :style=\"styles\" :title=\"title\">{{ body }}</p><!--safe--></section>" =>
                Element(
                    "section",
                    [ElementBinding.Attribute(new QualifiedName("id"), "root")],
                    [
                        Element("h1", children: [new ComponentTextNode("<heading>")]),
                        Element(
                            "p",
                            [
                                ElementBinding.Attribute(new QualifiedName("class"), classes),
                                ElementBinding.Attribute(new QualifiedName("style"), styles),
                                ElementBinding.Attribute(new QualifiedName("title"), "<heading>"),
                            ],
                            [new ComponentTextNode("body & text")]),
                        new ComponentCommentNode("safe"),
                    ]),
            "<span>{{ title }}</span><input disabled :title=\"body\">" =>
                new ComponentFragmentNode(
                [
                    Element("span", children: [new ComponentTextNode("<heading>")]),
                    Element(
                        "input",
                        [
                            ElementBinding.Attribute(new QualifiedName("disabled"), string.Empty),
                            ElementBinding.Attribute(new QualifiedName("title"), "body & text"),
                        ]),
                ]),
            "<textarea :value=\"body\">ignored</textarea>" =>
                Element(
                    "textarea",
                    [ElementBinding.Property("value", "body & text")],
                    [new ComponentTextNode("ignored")]),
            "<div v-if=\"visible\">yes</div>" => new ComponentCommentNode("v-if"),
            "<ul><li v-for=\"item in items\">{{ item }}</li></ul>" =>
                Element(
                    "ul",
                    children:
                    [
                        new ComponentFragmentNode(
                        [
                            Element("li", children: [new ComponentTextNode("one")]),
                            Element("li", children: [new ComponentTextNode("<two>")]),
                        ]),
                    ]),
            "<input v-model=\"modelText\">" =>
                Element(
                    "input",
                    [ElementBinding.Attribute(new QualifiedName("value"), "typed & ready")]),
            "<input type=\"checkbox\" value=\"yes\" v-model=\"checkboxModel\">" =>
                Element(
                    "input",
                    [
                        ElementBinding.Attribute(new QualifiedName("type"), "checkbox"),
                        ElementBinding.Attribute(new QualifiedName("value"), "yes"),
                        ElementBinding.Attribute(new QualifiedName("checked"), true),
                    ]),
            "<textarea v-model=\"body\">ignored</textarea>" =>
                Element(
                    "textarea",
                    [ElementBinding.Property("value", "body & text")],
                    [new ComponentTextNode("ignored")]),
            "<select v-model=\"choice\"><option value=\"one\">One</option><option :value=\"second\">Two</option></select>" =>
                Element(
                    "select",
                    children:
                    [
                        Element(
                            "option",
                            [ElementBinding.Attribute(new QualifiedName("value"), "one")],
                            [new ComponentTextNode("One")]),
                        Element(
                            "option",
                            [
                                ElementBinding.Attribute(new QualifiedName("value"), "two"),
                                ElementBinding.Attribute(new QualifiedName("selected"), true),
                            ],
                            [new ComponentTextNode("Two")]),
                    ]),
            "<select v-model=\"choice\"><option>{{ choice }}</option></select>" =>
                Element(
                    "select",
                    children:
                    [
                        Element(
                            "option",
                            [ElementBinding.Attribute(new QualifiedName("selected"), true)],
                            [new ComponentTextNode("two")]),
                    ]),
            "<div :style=\"styles\" v-show=\"visible\">hidden</div>" =>
                Element(
                    "div",
                    [
                        ElementBinding.Attribute(
                            new QualifiedName("style"),
                            "font-size:12px;color:red;display:none;"),
                    ],
                    [new ComponentTextNode("hidden")]),
            "<main><h1>heading</h1><p>body</p></main>" =>
                Element(
                    "main",
                    [ElementBinding.Attribute(new QualifiedName("data-v-c0ffee00"), string.Empty)],
                    [
                        Element(
                            "h1",
                            [ElementBinding.Attribute(
                                new QualifiedName("data-v-c0ffee00"),
                                string.Empty)],
                            [new ComponentTextNode("heading")]),
                        Element(
                            "p",
                            [ElementBinding.Attribute(
                                new QualifiedName("data-v-c0ffee00"),
                                string.Empty)],
                            [new ComponentTextNode("body")]),
                    ]),
            "<template v-if=\"!visible\"><span>one</span><span>two</span></template>" =>
                new ComponentFragmentNode(
                [
                    Element("span", children: [new ComponentTextNode("one")]),
                    Element("span", children: [new ComponentTextNode("two")]),
                ]),
            "<component :is=\"viewName\"><span>{{ body }}</span></component>" =>
                Element(
                    "aside",
                    children:
                    [
                        Element("span", children: [new ComponentTextNode("body & text")]),
                    ]),
            "<div v-bind=\"attributes\"><span>{{ body }}</span></div>" =>
                Element(
                    "div",
                    [
                        ElementBinding.Attribute(new QualifiedName("title"), "<fallback>"),
                        ElementBinding.Attribute(
                            new QualifiedName("data-v-fallback00"),
                            string.Empty),
                    ],
                    children:
                    [
                        Element(
                            "span",
                            [ElementBinding.Attribute(
                                new QualifiedName("data-v-fallback00"),
                                string.Empty)],
                            [new ComponentTextNode("body & text")]),
                    ]),
            "<input :value.prop=\"body\">" =>
                Element(
                    "input",
                    [ElementBinding.Property("value", "body & text")]),
            _ => throw new InvalidOperationException("Unknown differential fixture."),
        };
    }

    private static ComponentElementNode Element(
        string name,
        IReadOnlyList<ElementBinding>? bindings = null,
        IReadOnlyList<ComponentVirtualNode>? children = null) =>
        new(new QualifiedName(name), bindings, children);
}
