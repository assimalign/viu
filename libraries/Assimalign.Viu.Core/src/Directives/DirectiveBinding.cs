using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>Provides one resolved directive use to its lifecycle hooks.</summary>
public sealed class DirectiveBinding
{
    private Func<string, IReadOnlyList<DirectiveHostElement>>? _hostElements;

    internal DirectiveBinding(
        string directiveName,
        IDirective directive,
        IComponentContext? context,
        object? value,
        object? previousValue,
        string? argument,
        IReadOnlyDictionary<string, bool> modifiers)
    {
        DirectiveName = directiveName;
        Directive = directive;
        Context = context;
        Value = value;
        PreviousValue = previousValue;
        Argument = argument;
        Modifiers = modifiers;
    }

    /// <summary>Gets the application registration name used for this directive.</summary>
    public string DirectiveName { get; }

    /// <summary>Gets the resolved reusable directive.</summary>
    public IDirective Directive { get; }

    /// <summary>Gets the component context whose render attached this binding, when present.</summary>
    public IComponentContext? Context { get; }

    /// <summary>Gets the current bound value.</summary>
    public object? Value { get; }

    /// <summary>Gets the previous bound value on update, or null.</summary>
    public object? PreviousValue { get; }

    /// <summary>Gets the optional directive argument.</summary>
    public string? Argument { get; }

    /// <summary>Gets the immutable directive modifiers.</summary>
    public IReadOnlyDictionary<string, bool> Modifiers { get; }

    /// <summary>
    /// Gets the transition attached to the bound element, or null when the element is not inside a
    /// transition.
    /// </summary>
    public ComponentTransition? Transition { get; private set; }

    /// <summary>
    /// Gets mounted descendant host elements with the supplied tag in document order.
    /// </summary>
    /// <remarks>
    /// Descendants are available from the mounted hook through before-unmount. The created and
    /// before-mount phases return an empty list because child mounting has not completed.
    /// </remarks>
    /// <param name="tag">The platform tag name.</param>
    /// <returns>The matching component/host-element pairs.</returns>
    public IReadOnlyList<DirectiveHostElement> GetDescendantElements(string tag)
    {
        ArgumentException.ThrowIfNullOrEmpty(tag);
        return _hostElements?.Invoke(tag)
            ?? Array.Empty<DirectiveHostElement>();
    }

    internal void BindHostElements(
        Func<string, IReadOnlyList<DirectiveHostElement>> hostElements)
    {
        ArgumentNullException.ThrowIfNull(hostElements);
        _hostElements = hostElements;
    }

    internal void BindTransition(TransitionHooks? transition)
    {
        Transition = transition?.ComponentTransition;
    }
}
