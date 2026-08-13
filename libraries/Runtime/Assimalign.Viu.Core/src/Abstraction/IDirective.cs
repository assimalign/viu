namespace Assimalign.Viu;

/// <summary>Defines optional renderer lifecycle hooks for one reusable runtime directive.</summary>
/// <remarks>
/// Directive instances are resolved from compiler-emitted type tokens without reflection and are
/// borrowed by the application. Specified by <c>[CMP-7]</c>, <c>[APP-2]</c>, and <c>[APP-6]</c>.
/// </remarks>
public interface IDirective
{
    /// <summary>Gets the hook invoked after host-element creation and before host bindings.</summary>
    DirectiveHook? Created => null;

    /// <summary>Gets the hook invoked immediately before the host element is inserted.</summary>
    DirectiveHook? BeforeMount => null;

    /// <summary>Gets the post-flush hook invoked after the host element is inserted.</summary>
    DirectiveHook? Mounted => null;

    /// <summary>Gets the hook invoked before an existing host element is patched.</summary>
    DirectiveHook? BeforeUpdate => null;

    /// <summary>Gets the post-flush hook invoked after an existing host element is patched.</summary>
    DirectiveHook? Updated => null;

    /// <summary>Gets the hook invoked before the bound host element is unmounted.</summary>
    DirectiveHook? BeforeUnmount => null;

    /// <summary>Gets the post-flush hook invoked after the bound host element is unmounted.</summary>
    DirectiveHook? Unmounted => null;
}
