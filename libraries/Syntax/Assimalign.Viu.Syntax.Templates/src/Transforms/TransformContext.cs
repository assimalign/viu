using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The mutable state threaded through the transform pipeline: helper/component/directive registration, scope
/// tracking, node replacement/removal, and the hoist/cache hooks later stages consume.
/// </summary>
/// <remarks>
/// This type is single-threaded, matching the compiler's single-pass model. Because the parse AST is
/// immutable, per-node code-generation results are held in reference-keyed side tables rather than mutated
/// onto the nodes, and structural containers use their mutable working forms while traversal is in flight.
/// </remarks>
public sealed class TransformContext
{
    private readonly Dictionary<RuntimeHelper, int> helpers = new();
    private readonly HashSet<string> components = new();
    private readonly HashSet<string> directives = new();
    private readonly List<TemplateSyntaxNode?> hoists = new();
    private readonly List<CacheExpression?> cached = new();
    private readonly Dictionary<TemplateSyntaxNode, TemplateSyntaxNode> codegenNodes = new(ReferenceComparer.Instance);
    private readonly Dictionary<TemplateSyntaxNode, List<TemplateSyntaxNode>> workingChildren = new(ReferenceComparer.Instance);
    private readonly HashSet<TemplateSyntaxNode> seenOnce = new(ReferenceComparer.Instance);
    private readonly HashSet<TemplateSyntaxNode> seenMemo = new(ReferenceComparer.Instance);
    private readonly Dictionary<TemplateSyntaxNode, RuntimeHelper> directiveRuntime = new(ReferenceComparer.Instance);
    private readonly Dictionary<string, int> identifiers = new();
    private readonly Dictionary<TemplateSyntaxNode, ConstantType> constantCache = new(ReferenceComparer.Instance);

    internal TransformContext(
        RootNode root,
        IReadOnlyList<NodeTransform> nodeTransforms,
        IReadOnlyDictionary<string, DirectiveTransform> directiveTransforms,
        TransformOptions options)
    {
        Root = root;
        NodeTransforms = nodeTransforms;
        DirectiveTransforms = directiveTransforms;
        IsBuiltInComponent = options.IsBuiltInComponent;
        IsCustomElement = options.IsCustomElement;
        CacheHandlers = options.CacheHandlers;
        PrefixIdentifiers = options.PrefixIdentifiers;
        BindingMetadata = options.BindingMetadata ?? BindingMetadata.Empty;
        CssModules = options.CssModules ?? CssModuleAccessors.Empty;
        BindingRewriteMode = options.BindingRewriteMode;
        IsNestedServerRendering = options.IsNestedServerRendering;
        IsServerRendering = options.IsServerRendering;
        Slotted = options.Slotted;
        ScopeId = options.ScopeId;
        onError = options.OnError;
        CurrentNode = root;
        OnNodeRemoved = static () => { };
    }

    private readonly Action<CompilerError>? onError;

    /// <summary>The root of the template being transformed.</summary>
    public RootNode Root { get; }

    /// <summary>The resolved, ordered node transforms (built-in preset then user transforms).</summary>
    public IReadOnlyList<NodeTransform> NodeTransforms { get; }

    /// <summary>The resolved directive transforms, keyed by directive name.</summary>
    public IReadOnlyDictionary<string, DirectiveTransform> DirectiveTransforms { get; }

    /// <summary>Resolves a built-in component tag to its runtime helper, or <see langword="null"/>.</summary>
    public Func<string, RuntimeHelper?>? IsBuiltInComponent { get; }

    /// <summary>Whether a tag is a custom element.</summary>
    public Func<string, bool> IsCustomElement { get; }

    /// <summary>Whether static event handlers are cached.</summary>
    public bool CacheHandlers { get; }

    /// <summary>
    /// Whether identifier prefixing and scope analysis is enabled. When
    /// <see langword="false"/> expression bodies stay opaque; when
    /// <see langword="true"/> the <see cref="TransformExpression"/> pass rewrites identifiers ([V01.01.05.04]).
    /// </summary>
    public bool PrefixIdentifiers { get; }

    /// <summary>
    /// The component binding classifications expression rewriting resolves identifiers against.
    /// Defaults to <see cref="BindingMetadata.Empty"/>.
    /// </summary>
    public BindingMetadata BindingMetadata { get; }

    /// <summary>
    /// The CSS Modules accessors ([V01.01.05.04.01]) expression classification resolves <c>$style</c> (and
    /// named-module) references against. Defaults to <see cref="CssModuleAccessors.Empty"/>.
    /// </summary>
    public CssModuleAccessors CssModules { get; }

    /// <summary>How a rewritten binding spells its receiver. Defaults to <see cref="BindingRewriteMode.RenderContext"/>.</summary>
    public BindingRewriteMode BindingRewriteMode { get; }

    /// <summary>Whether this is a nested SSR slot compilation.</summary>
    public bool IsNestedServerRendering { get; }

    /// <summary>Whether compilation targets SSR.</summary>
    public bool IsServerRendering { get; }

    /// <summary>Whether component slots inherit the parent scope id.</summary>
    public bool Slotted { get; }

    /// <summary>The scoped-styles id, or <see langword="null"/>.</summary>
    public string? ScopeId { get; }

    /// <summary>The number of <c>v-for</c> scopes currently open.</summary>
    public int ScopeVFor { get; set; }

    /// <summary>The number of <c>v-slot</c> scopes currently open.</summary>
    public int ScopeVSlot { get; set; }

    /// <summary>Whether transformation is inside a <c>v-once</c> subtree.</summary>
    public bool InVOnce { get; set; }

    // ---- traversal state ----

    /// <summary>The node currently being transformed, or <see langword="null"/> after removal.</summary>
    public TemplateSyntaxNode? CurrentNode { get; internal set; }

    /// <summary>The parent of <see cref="CurrentNode"/>.</summary>
    public TemplateSyntaxNode? Parent { get; internal set; }

    /// <summary>The mutable working children list of <see cref="Parent"/> (the current sibling list).</summary>
    internal List<TemplateSyntaxNode>? CurrentChildren { get; set; }

    /// <summary>The index of <see cref="CurrentNode"/> within <see cref="CurrentChildren"/>.</summary>
    public int ChildIndex { get; internal set; }

    /// <summary>The callback the traversal installs so removals keep the loop index aligned.</summary>
    internal Action OnNodeRemoved { get; set; }

    // ---- diagnostics ----

    /// <summary>Reports a transform diagnostic (never throws; recoverable).</summary>
    /// <param name="error">The diagnostic to report.</param>
    public void ReportError(CompilerError error) => onError?.Invoke(error);

    // ---- template-local identifier scope ----

    /// <summary>
    /// Whether <paramref name="name"/> is currently a template-local (a <c>v-for</c> alias or <c>v-slot</c> prop
    /// in scope). Such identifiers shadow component bindings and are never prefixed or unwrapped.
    /// </summary>
    /// <param name="name">The identifier name.</param>
    public bool IsLocalIdentifier(string name) => identifiers.TryGetValue(name, out var count) && count > 0;

    /// <summary>Pushes <paramref name="name"/> onto the local scope.</summary>
    /// <param name="name">The identifier name.</param>
    public void AddIdentifiers(string name)
    {
        identifiers.TryGetValue(name, out var count);
        identifiers[name] = count + 1;
    }

    /// <summary>
    /// Pushes the identifiers <paramref name="expression"/> declares onto the local scope — a plain alias, a
    /// tuple, or a deconstruction.
    /// </summary>
    /// <param name="expression">The alias or slot-prop expression.</param>
    public void AddIdentifiers(ExpressionNode expression)
    {
        foreach (var name in DeclaredIdentifiersOf(expression))
        {
            AddIdentifiers(name);
        }
    }

    /// <summary>Pops <paramref name="name"/> from the local scope.</summary>
    /// <param name="name">The identifier name.</param>
    public void RemoveIdentifiers(string name)
    {
        if (identifiers.TryGetValue(name, out var count))
        {
            if (count <= 1)
            {
                identifiers.Remove(name);
            }
            else
            {
                identifiers[name] = count - 1;
            }
        }
    }

    /// <summary>Pops the identifiers <paramref name="expression"/> declares.</summary>
    /// <param name="expression">The alias or slot-prop expression.</param>
    public void RemoveIdentifiers(ExpressionNode expression)
    {
        foreach (var name in DeclaredIdentifiersOf(expression))
        {
            RemoveIdentifiers(name);
        }
    }

    private static IReadOnlyList<string> DeclaredIdentifiersOf(ExpressionNode expression)
        => expression is SimpleExpressionNode simple
            ? IdentifierExtraction.CollectDeclaredIdentifiers(simple.Content)
            : System.Array.Empty<string>();

    // ---- helper / component / directive registration ----

    /// <summary>Registers a use of <paramref name="helper"/> and returns it.</summary>
    /// <param name="helper">The runtime helper.</param>
    public RuntimeHelper Helper(RuntimeHelper helper)
    {
        helpers.TryGetValue(helper, out var count);
        helpers[helper] = count + 1;
        return helper;
    }

    /// <summary>Removes a use of <paramref name="helper"/>.</summary>
    /// <param name="helper">The runtime helper.</param>
    public void RemoveHelper(RuntimeHelper helper)
    {
        if (helpers.TryGetValue(helper, out var count) && count > 0)
        {
            if (count - 1 == 0)
            {
                helpers.Remove(helper);
            }
            else
            {
                helpers[helper] = count - 1;
            }
        }
    }

    /// <summary>
    /// Registers <paramref name="helper"/> and returns its underscore-prefixed intermediate marker.
    /// The frame render writer consumes the marker and emits the corresponding direct API expression;
    /// it is never retained as a consumer runtime lookup.
    /// </summary>
    /// <param name="helper">The symbolic transform operation.</param>
    public string HelperString(RuntimeHelper helper) => "_" + Helper(helper).Name;

    /// <summary>Registers a resolved component name.</summary>
    /// <param name="name">The component tag name.</param>
    public void AddComponent(string name) => components.Add(name);

    /// <summary>Registers a resolved directive name.</summary>
    /// <param name="name">The directive name.</param>
    public void AddDirective(string name) => directives.Add(name);

    // ---- node replacement / removal ----

    /// <summary>Replaces the current node with <paramref name="node"/>.</summary>
    /// <param name="node">The replacement node.</param>
    public void ReplaceNode(TemplateSyntaxNode node)
    {
        if (CurrentChildren is not null)
        {
            CurrentChildren[ChildIndex] = node;
        }

        CurrentNode = node;
    }

    /// <summary>Removes <paramref name="node"/> (or the current node) from the sibling list.</summary>
    /// <param name="node">The node to remove, or <see langword="null"/> for the current node.</param>
    public void RemoveNode(TemplateSyntaxNode? node = null)
    {
        var list = CurrentChildren;
        if (list is null)
        {
            return;
        }

        var removalIndex = node is not null
            ? ReferenceIndexOf(list, node)
            : CurrentNode is not null ? ChildIndex : -1;
        if (removalIndex < 0)
        {
            return;
        }

        if (node is null || ReferenceEquals(node, CurrentNode))
        {
            CurrentNode = null;
            OnNodeRemoved();
        }
        else if (ChildIndex > removalIndex)
        {
            ChildIndex--;
            OnNodeRemoved();
        }

        list.RemoveAt(removalIndex);
    }

    // ---- hoist / cache hooks ([V01.01.05.07] consumes these) ----

    /// <summary>
    /// Registers a hoisted constant and returns a placeholder identifier. The hoisting
    /// pass itself is [V01.01.05.07]; this records the slot so that pass can plug in without reshaping the
    /// pipeline.
    /// </summary>
    /// <param name="expression">The constant expression to hoist.</param>
    public SimpleExpressionNode Hoist(TemplateSyntaxNode expression)
    {
        hoists.Add(expression);
        return Ir.SimpleExpression(
            $"_hoisted_{hoists.Count}",
            false,
            expression.Location,
            ConstantType.CanCache);
    }

    /// <summary>Wraps <paramref name="expression"/> in a cache slot.</summary>
    /// <param name="expression">The expression to cache.</param>
    /// <param name="isVirtualNode">Whether the cached value is a virtual node.</param>
    /// <param name="inVOnce">Whether the cache is produced inside a <c>v-once</c>.</param>
    public CacheExpression Cache(TemplateSyntaxNode expression, bool isVirtualNode = false, bool inVOnce = false)
    {
        var cacheExpression = Ir.CacheExpression(cached.Count, expression, isVirtualNode, inVOnce);
        cached.Add(cacheExpression);
        return cacheExpression;
    }

    /// <summary>The current cache slot count.</summary>
    public int CacheCount => cached.Count;

    /// <summary>Reserves an empty cache slot, incrementing the count.</summary>
    public void AppendEmptyCacheSlot() => cached.Add(null);

    // ---- virtual-node-call construction ----

    /// <summary>
    /// Builds a <see cref="VirtualNodeCall"/> and registers the block/render-node helpers it needs, so a caller
    /// can never construct one that references a helper the emitter did not import.
    /// </summary>
    public VirtualNodeCall CreateVirtualNodeCall(
        object tag,
        TemplateSyntaxNode? properties,
        object? children,
        PatchFlags? patchFlag,
        object? dynamicProperties,
        ArrayExpression? directiveArguments,
        bool isBlock,
        bool disableTracking,
        bool isComponent,
        SourceLocation? location = null)
    {
        if (isBlock)
        {
            Helper(HelperNames.OpenBlock);
            Helper(GetVirtualNodeBlockHelper(IsNestedServerRendering, isComponent));
        }
        else
        {
            Helper(GetVirtualNodeHelper(IsNestedServerRendering, isComponent));
        }

        if (directiveArguments is not null)
        {
            Helper(HelperNames.WithDirectives);
        }

        return new VirtualNodeCall
        {
            Tag = tag,
            Properties = properties,
            Children = children,
            PatchFlag = patchFlag,
            DynamicProperties = dynamicProperties,
            Directives = directiveArguments,
            IsBlock = isBlock,
            DisableTracking = disableTracking,
            IsComponent = isComponent,
            Location = location ?? Ir.LocationStub,
        };
    }

    /// <summary>The virtual-node-creation helper for the given server-rendering and component flags.</summary>
    public static RuntimeHelper GetVirtualNodeHelper(bool isServerRendering, bool isComponent)
        => isServerRendering || isComponent ? HelperNames.CreateVNode : HelperNames.CreateElementVNode;

    /// <summary>The block-virtual-node-creation helper for the given server-rendering and component flags.</summary>
    public static RuntimeHelper GetVirtualNodeBlockHelper(bool isServerRendering, bool isComponent)
        => isServerRendering || isComponent ? HelperNames.CreateBlock : HelperNames.CreateElementBlock;

    // ---- per-node code-generation side tables (immutable AST cannot carry a codegenNode) ----

    internal void SetCodegenNode(TemplateSyntaxNode node, TemplateSyntaxNode codegenNode)
    {
        switch (node)
        {
            case WorkingIf workingIf:
                workingIf.CodegenNode = codegenNode;
                break;
            case WorkingFor workingFor:
                workingFor.CodegenNode = codegenNode;
                break;
            default:
                codegenNodes[node] = codegenNode;
                break;
        }
    }

    internal TemplateSyntaxNode? GetCodegenNode(TemplateSyntaxNode node) => node switch
    {
        WorkingIf workingIf => workingIf.CodegenNode,
        WorkingFor workingFor => workingFor.CodegenNode,
        _ => codegenNodes.TryGetValue(node, out var codegenNode) ? codegenNode : null,
    };

    internal List<TemplateSyntaxNode> WorkingChildrenOf(TemplateSyntaxNode node, IReadOnlyList<TemplateChildNode> initial)
    {
        if (!workingChildren.TryGetValue(node, out var list))
        {
            list = new List<TemplateSyntaxNode>(initial.Count);
            for (var index = 0; index < initial.Count; index++)
            {
                list.Add(initial[index]);
            }

            workingChildren[node] = list;
        }

        return list;
    }

    internal bool TryGetWorkingChildren(TemplateSyntaxNode node, out List<TemplateSyntaxNode> children)
        => workingChildren.TryGetValue(node, out children!);

    internal HashSet<TemplateSyntaxNode> SeenOnce => seenOnce;

    internal HashSet<TemplateSyntaxNode> SeenMemo => seenMemo;

    // The memoization table for the static-caching pass's element constant analysis. Keyed by node
    // reference so a subtree's constant type is computed once; [V01.01.05.07] populates it.
    internal Dictionary<TemplateSyntaxNode, ConstantType> ConstantCache => constantCache;

    // Records the runtime helper a directive transform requested, so the element transform can emit a
    // helper reference instead of a resolveDirective call.
    internal void SetDirectiveRuntime(DirectiveNode directive, RuntimeHelper helper) => directiveRuntime[directive] = helper;

    internal RuntimeHelper? GetDirectiveRuntime(DirectiveNode directive)
        => directiveRuntime.TryGetValue(directive, out var helper) ? helper : null;

    // ---- finalize accessors (the facade reads these after traversal) ----

    internal IReadOnlyCollection<RuntimeHelper> HelperKeys => helpers.Keys;

    internal IReadOnlyCollection<string> ComponentNames => components;

    internal IReadOnlyCollection<string> DirectiveNames => directives;

    internal IReadOnlyList<TemplateSyntaxNode?> Hoists => hoists;

    internal IReadOnlyList<CacheExpression?> CachedSlots => cached;

    private static int ReferenceIndexOf(List<TemplateSyntaxNode> list, TemplateSyntaxNode node)
    {
        for (var index = 0; index < list.Count; index++)
        {
            if (ReferenceEquals(list[index], node))
            {
                return index;
            }
        }

        return -1;
    }
}
