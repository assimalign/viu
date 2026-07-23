namespace Assimalign.Viu;

/// <summary>
/// Defines the optional renderer lifecycle hooks for one reusable runtime directive.
/// </summary>
/// <remarks>
/// This is Viu's host-neutral counterpart to Vue's object directive:
/// https://vuejs.org/guide/reusability/custom-directives.html. Hooks receive the boxed host node
/// because the application registry is independent of a renderer's <c>TNode</c>.
/// </remarks>
public interface IDirective
{
    /// <summary>Gets the hook invoked after element creation and before attributes are applied.</summary>
    DirectiveHook? Created => null;

    /// <summary>Gets the hook invoked immediately before the element is inserted.</summary>
    DirectiveHook? BeforeMount => null;

    /// <summary>Gets the post-flush hook invoked after the element is inserted.</summary>
    DirectiveHook? Mounted => null;

    /// <summary>Gets the hook invoked before an existing element is patched.</summary>
    DirectiveHook? BeforeUpdate => null;

    /// <summary>Gets the post-flush hook invoked after an existing element is patched.</summary>
    DirectiveHook? Updated => null;

    /// <summary>Gets the hook invoked before the bound element is unmounted.</summary>
    DirectiveHook? BeforeUnmount => null;

    /// <summary>Gets the post-flush hook invoked after the bound element is unmounted.</summary>
    DirectiveHook? Unmounted => null;
}
