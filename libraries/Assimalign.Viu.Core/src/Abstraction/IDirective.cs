namespace Assimalign.Viu;

/// <summary>
/// A runtime custom directive — the C# port of upstream's <c>ObjectDirective</c>
/// (<c>packages/runtime-core/src/directives.ts</c>,
/// https://vuejs.org/guide/reusability/custom-directives.html): a bundle of optional lifecycle
/// hooks the renderer invokes on the element the directive is bound to. Every hook defaults to
/// null (no dispatch), so a directive implements only the phases it needs and the renderer skips
/// the rest — mirroring upstream's <c>if (hook)</c> guard. Register directives on the app by name
/// (<see cref="Application{TNode}.Directive(string, IDirective)"/>) or attach them directly with
/// <see cref="Directives.WithDirectives(VirtualNode, DirectiveArgument[])"/>. Implement this
/// interface for a stateful directive, or use the <see cref="Directive"/> record for a bundle of
/// lambdas. Hook dispatch is direct invocation — never reflection over hook names (AOT/trimming
/// contract).
/// </summary>
public interface IDirective
{
    /// <summary>Runs once, before the bound element's attributes/event listeners are applied (upstream: <c>created</c>).</summary>
    DirectiveHook? Created => null;

    /// <summary>Runs before the bound element is inserted into its parent (upstream: <c>beforeMount</c>).</summary>
    DirectiveHook? BeforeMount => null;

    /// <summary>Runs after the bound element and its parent's subtree are mounted — post-flush (upstream: <c>mounted</c>).</summary>
    DirectiveHook? Mounted => null;

    /// <summary>Runs before the containing component's vnode updates the bound element (upstream: <c>beforeUpdate</c>).</summary>
    DirectiveHook? BeforeUpdate => null;

    /// <summary>Runs after the containing component's vnode and its children have updated — post-flush (upstream: <c>updated</c>).</summary>
    DirectiveHook? Updated => null;

    /// <summary>Runs before the bound element is removed (upstream: <c>beforeUnmount</c>).</summary>
    DirectiveHook? BeforeUnmount => null;

    /// <summary>Runs after the bound element is removed — post-flush (upstream: <c>unmounted</c>).</summary>
    DirectiveHook? Unmounted => null;
}
