using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>Provides one resolved directive use to its lifecycle hooks.</summary>
/// <remarks>Specified by <c>[CMP-7]</c> and <c>[HYD-4]</c>.</remarks>
public sealed class DirectiveBinding
{
    private Func<string, IReadOnlyList<DirectiveHostElement>>? _hostElements;

    internal DirectiveBinding(
        Type directiveType,
        IDirective directive,
        ComponentContext? context,
        object? value,
        object? previousValue)
    {
        DirectiveType = directiveType;
        Directive = directive;
        Context = context;
        Value = value;
        PreviousValue = previousValue;
    }

    /// <summary>Gets the compile-time-known directive type token.</summary>
    public Type DirectiveType { get; }

    /// <summary>Gets the resolved reusable directive.</summary>
    public IDirective Directive { get; }

    /// <summary>Gets the component context whose render attached the directive, when present.</summary>
    public ComponentContext? Context { get; }

    /// <summary>Gets the value captured for the current render.</summary>
    public object? Value { get; }

    /// <summary>Gets the value captured for the previous render, or null.</summary>
    public object? PreviousValue { get; }

    /// <summary>
    /// Gets the transition bound to this element, or null when no structural transition owns it.
    /// </summary>
    /// <remarks>Persisted directives use this host-neutral seam as specified by <c>[BLT-10]</c>.</remarks>
    public ComponentTransition? Transition { get; private set; }

    /// <summary>Gets mounted descendant host elements with the supplied local name in tree order.</summary>
    /// <param name="localName">The non-empty host-element local name.</param>
    /// <returns>The matching immutable-node and host-element pairs.</returns>
    public IReadOnlyList<DirectiveHostElement> GetDescendantElements(string localName)
    {
        ArgumentException.ThrowIfNullOrEmpty(localName);
        return _hostElements?.Invoke(localName) ?? Array.Empty<DirectiveHostElement>();
    }

    internal void BindHostElements(
        Func<string, IReadOnlyList<DirectiveHostElement>> hostElements)
    {
        ArgumentNullException.ThrowIfNull(hostElements);
        _hostElements = hostElements;
    }

    internal void BindTransition(ComponentTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        Transition = transition;
    }
}
