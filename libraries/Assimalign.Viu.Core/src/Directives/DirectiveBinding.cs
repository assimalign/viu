using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Assimalign.Viu;

/// <summary>
/// The per-use state of a directive on one vnode — the C# port of upstream's
/// <c>DirectiveBinding</c> (<c>packages/runtime-core/src/directives.ts</c>,
/// https://vuejs.org/guide/reusability/custom-directives.html#directive-hooks). Passed to every
/// hook so it can read the bound value, the argument, and the modifiers, and reach the component
/// instance whose render attached it. Created by
/// <see cref="Directives.WithDirectives(VirtualNode, DirectiveArgument[])"/>.
/// </summary>
public sealed class DirectiveBinding
{
    /// <summary>The shared empty modifier set (upstream: <c>EMPTY_OBJ</c>).</summary>
    internal static readonly IReadOnlyDictionary<string, bool> EmptyModifiers = ReadOnlyDictionary<string, bool>.Empty;

    internal DirectiveBinding(
        IDirective directive,
        ComponentInstance? instance,
        object? value,
        string? argument,
        IReadOnlyDictionary<string, bool> modifiers)
    {
        Directive = directive;
        Instance = instance;
        Value = value;
        Argument = argument;
        Modifiers = modifiers;
    }

    /// <summary>The directive whose hooks this binding drives (upstream: <c>binding.dir</c>).</summary>
    public IDirective Directive { get; }

    /// <summary>
    /// The component instance whose render attached the directive (upstream: <c>binding.instance</c>).
    /// </summary>
    public ComponentInstance? Instance { get; }

    /// <summary>The current bound value (upstream: <c>binding.value</c>).</summary>
    public object? Value { get; }

    /// <summary>
    /// The value from the previous render, set on the update hooks (upstream: <c>binding.oldValue</c>);
    /// null on the created/mount hooks.
    /// </summary>
    public object? OldValue { get; internal set; }

    /// <summary>
    /// The directive argument (upstream: <c>binding.arg</c> — the <c>foo</c> in <c>v-x:foo</c>),
    /// or null.
    /// </summary>
    public string? Argument { get; }

    /// <summary>
    /// The directive modifiers (upstream: <c>binding.modifiers</c> — the <c>bar</c> in
    /// <c>v-x.bar</c>, present with value true); an empty set when there are none.
    /// </summary>
    public IReadOnlyDictionary<string, bool> Modifiers { get; }
}
