using System;
using System.Collections.Generic;

using Assimalign.Vue.Shared;

namespace Assimalign.Vue.Compiler;

/// <summary>
/// The mutable state threaded through the transform pipeline: helper/component/directive registration, scope
/// tracking, node replacement/removal, and the hoist/cache hooks later stages consume. The C# port of Vue
/// 3.5's <c>TransformContext</c> (<c>@vue/compiler-core</c> <c>transform.ts</c>).
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
    private readonly List<SyntaxNode?> hoists = new();
    private readonly List<CacheExpression?> cached = new();
    private readonly Dictionary<SyntaxNode, SyntaxNode> codegenNodes = new(ReferenceComparer.Instance);
    private readonly Dictionary<SyntaxNode, List<SyntaxNode>> workingChildren = new(ReferenceComparer.Instance);
    private readonly HashSet<SyntaxNode> seenOnce = new(ReferenceComparer.Instance);
    private readonly HashSet<SyntaxNode> seenMemo = new(ReferenceComparer.Instance);
    private readonly Dictionary<SyntaxNode, RuntimeHelper> directiveRuntime = new(ReferenceComparer.Instance);

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
        InSSR = options.InSSR;
        Ssr = options.Ssr;
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

    /// <summary>Whether static event handlers are cached (upstream <c>cacheHandlers</c>).</summary>
    public bool CacheHandlers { get; }

    /// <summary>
    /// Whether identifier prefixing is enabled. Always <see langword="false"/> in this build; expression and
    /// scope analysis is [V01.01.05.04].
    /// </summary>
    public bool PrefixIdentifiers => false;

    /// <summary>Whether this is a nested SSR slot compilation.</summary>
    public bool InSSR { get; }

    /// <summary>Whether compilation targets SSR.</summary>
    public bool Ssr { get; }

    /// <summary>Whether component slots inherit the parent scope id.</summary>
    public bool Slotted { get; }

    /// <summary>The scoped-styles id, or <see langword="null"/>.</summary>
    public string? ScopeId { get; }

    /// <summary>The number of <c>v-for</c> scopes currently open (upstream <c>scopes.vFor</c>).</summary>
    public int ScopeVFor { get; set; }

    /// <summary>The number of <c>v-slot</c> scopes currently open (upstream <c>scopes.vSlot</c>).</summary>
    public int ScopeVSlot { get; set; }

    /// <summary>Whether transformation is inside a <c>v-once</c> subtree (upstream <c>inVOnce</c>).</summary>
    public bool InVOnce { get; set; }

    // ---- traversal state ----

    /// <summary>The node currently being transformed, or <see langword="null"/> after removal.</summary>
    public SyntaxNode? CurrentNode { get; internal set; }

    /// <summary>The parent of <see cref="CurrentNode"/>.</summary>
    public SyntaxNode? Parent { get; internal set; }

    /// <summary>The mutable working children list of <see cref="Parent"/> (the current sibling list).</summary>
    internal List<SyntaxNode>? CurrentChildren { get; set; }

    /// <summary>The index of <see cref="CurrentNode"/> within <see cref="CurrentChildren"/>.</summary>
    public int ChildIndex { get; internal set; }

    /// <summary>The callback the traversal installs so removals keep the loop index aligned.</summary>
    internal Action OnNodeRemoved { get; set; }

    // ---- diagnostics ----

    /// <summary>Reports a transform diagnostic (never throws; recoverable), matching upstream's <c>onError</c>.</summary>
    /// <param name="error">The diagnostic to report.</param>
    public void ReportError(CompilerError error) => onError?.Invoke(error);

    // ---- helper / component / directive registration ----

    /// <summary>Registers a use of <paramref name="helper"/> and returns it (upstream <c>helper</c>).</summary>
    /// <param name="helper">The runtime helper.</param>
    public RuntimeHelper Helper(RuntimeHelper helper)
    {
        helpers.TryGetValue(helper, out var count);
        helpers[helper] = count + 1;
        return helper;
    }

    /// <summary>Removes a use of <paramref name="helper"/> (upstream <c>removeHelper</c>).</summary>
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

    /// <summary>Registers and returns the <c>_name</c> reference string for <paramref name="helper"/> (upstream <c>helperString</c>).</summary>
    /// <param name="helper">The runtime helper.</param>
    public string HelperString(RuntimeHelper helper) => "_" + Helper(helper).Name;

    /// <summary>Registers a resolved component name (upstream <c>components.add</c>).</summary>
    /// <param name="name">The component tag name.</param>
    public void AddComponent(string name) => components.Add(name);

    /// <summary>Registers a resolved directive name (upstream <c>directives.add</c>).</summary>
    /// <param name="name">The directive name.</param>
    public void AddDirective(string name) => directives.Add(name);

    // ---- node replacement / removal ----

    /// <summary>Replaces the current node with <paramref name="node"/> (upstream <c>replaceNode</c>).</summary>
    /// <param name="node">The replacement node.</param>
    public void ReplaceNode(SyntaxNode node)
    {
        if (CurrentChildren is not null)
        {
            CurrentChildren[ChildIndex] = node;
        }

        CurrentNode = node;
    }

    /// <summary>Removes <paramref name="node"/> (or the current node) from the sibling list (upstream <c>removeNode</c>).</summary>
    /// <param name="node">The node to remove, or <see langword="null"/> for the current node.</param>
    public void RemoveNode(SyntaxNode? node = null)
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
    /// Registers a hoisted constant and returns a placeholder identifier (upstream <c>hoist</c>). The hoisting
    /// pass itself is [V01.01.05.07]; this records the slot so that pass can plug in without reshaping the
    /// pipeline.
    /// </summary>
    /// <param name="expression">The constant expression to hoist.</param>
    public SimpleExpressionNode Hoist(SyntaxNode expression)
    {
        hoists.Add(expression);
        return Ir.SimpleExpression(
            $"_hoisted_{hoists.Count}",
            false,
            expression.Location,
            ConstantType.CanCache);
    }

    /// <summary>Wraps <paramref name="expression"/> in a cache slot (upstream <c>cache</c>).</summary>
    /// <param name="expression">The expression to cache.</param>
    /// <param name="isVNode">Whether the cached value is a vnode.</param>
    /// <param name="inVOnce">Whether the cache is produced inside a <c>v-once</c>.</param>
    public CacheExpression Cache(SyntaxNode expression, bool isVNode = false, bool inVOnce = false)
    {
        var cacheExpression = Ir.CacheExpression(cached.Count, expression, isVNode, inVOnce);
        cached.Add(cacheExpression);
        return cacheExpression;
    }

    /// <summary>The current cache slot count (upstream <c>context.cached.length</c>).</summary>
    public int CacheCount => cached.Count;

    /// <summary>Reserves an empty cache slot, incrementing the count (upstream <c>context.cached.push(null)</c>).</summary>
    public void AppendEmptyCacheSlot() => cached.Add(null);

    // ---- vnode-call construction ----

    /// <summary>
    /// Builds a <see cref="VNodeCall"/> and registers the block/vnode helpers it needs (upstream
    /// <c>createVNodeCall</c>).
    /// </summary>
    public VNodeCall CreateVNodeCall(
        object tag,
        SyntaxNode? props,
        object? children,
        PatchFlags? patchFlag,
        object? dynamicProps,
        ArrayExpression? directiveArguments,
        bool isBlock,
        bool disableTracking,
        bool isComponent,
        SourceLocation? location = null)
    {
        if (isBlock)
        {
            Helper(HelperNames.OpenBlock);
            Helper(GetVNodeBlockHelper(InSSR, isComponent));
        }
        else
        {
            Helper(GetVNodeHelper(InSSR, isComponent));
        }

        if (directiveArguments is not null)
        {
            Helper(HelperNames.WithDirectives);
        }

        return new VNodeCall
        {
            Tag = tag,
            Props = props,
            Children = children,
            PatchFlag = patchFlag,
            DynamicProps = dynamicProps,
            Directives = directiveArguments,
            IsBlock = isBlock,
            DisableTracking = disableTracking,
            IsComponent = isComponent,
            Location = location ?? Ir.LocationStub,
        };
    }

    /// <summary>The vnode-creation helper for the given SSR/component flags (upstream <c>getVNodeHelper</c>).</summary>
    public static RuntimeHelper GetVNodeHelper(bool ssr, bool isComponent)
        => ssr || isComponent ? HelperNames.CreateVNode : HelperNames.CreateElementVNode;

    /// <summary>The block-vnode-creation helper for the given SSR/component flags (upstream <c>getVNodeBlockHelper</c>).</summary>
    public static RuntimeHelper GetVNodeBlockHelper(bool ssr, bool isComponent)
        => ssr || isComponent ? HelperNames.CreateBlock : HelperNames.CreateElementBlock;

    // ---- per-node code-generation side tables (immutable AST cannot carry a codegenNode) ----

    internal void SetCodegenNode(SyntaxNode node, SyntaxNode codegenNode)
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

    internal SyntaxNode? GetCodegenNode(SyntaxNode node) => node switch
    {
        WorkingIf workingIf => workingIf.CodegenNode,
        WorkingFor workingFor => workingFor.CodegenNode,
        _ => codegenNodes.TryGetValue(node, out var codegenNode) ? codegenNode : null,
    };

    internal List<SyntaxNode> WorkingChildrenOf(SyntaxNode node, IReadOnlyList<TemplateChildNode> initial)
    {
        if (!workingChildren.TryGetValue(node, out var list))
        {
            list = new List<SyntaxNode>(initial.Count);
            for (var index = 0; index < initial.Count; index++)
            {
                list.Add(initial[index]);
            }

            workingChildren[node] = list;
        }

        return list;
    }

    internal bool TryGetWorkingChildren(SyntaxNode node, out List<SyntaxNode> children)
        => workingChildren.TryGetValue(node, out children!);

    internal HashSet<SyntaxNode> SeenOnce => seenOnce;

    internal HashSet<SyntaxNode> SeenMemo => seenMemo;

    // The C# analogue of upstream's directiveImportMap: records the runtime helper a directive transform
    // requested, so the element transform can emit a helper reference instead of a resolveDirective call.
    internal void SetDirectiveRuntime(DirectiveNode directive, RuntimeHelper helper) => directiveRuntime[directive] = helper;

    internal RuntimeHelper? GetDirectiveRuntime(DirectiveNode directive)
        => directiveRuntime.TryGetValue(directive, out var helper) ? helper : null;

    // ---- finalize accessors (the facade reads these after traversal) ----

    internal IReadOnlyCollection<RuntimeHelper> HelperKeys => helpers.Keys;

    internal IReadOnlyCollection<string> ComponentNames => components;

    internal IReadOnlyCollection<string> DirectiveNames => directives;

    internal IReadOnlyList<SyntaxNode?> Hoists => hoists;

    internal IReadOnlyList<CacheExpression?> CachedSlots => cached;

    private static int ReferenceIndexOf(List<SyntaxNode> list, SyntaxNode node)
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
