using System;
using System.Collections;
using System.Collections.Generic;

using Assimalign.Viu;
using Assimalign.Viu.Shared;

namespace Assimalign.Viu.RuntimeDom;

/// <summary>
/// Shared helpers for the DOM <c>v-model</c> directives — unboxing the element handle, reading the
/// <see cref="ViuModelBinding"/> carrier and the directive modifiers, and formatting values for the
/// DOM the same invariant way the patch engine does. Keeps <see cref="VModelText"/> and its
/// siblings free of duplicated boilerplate; every operation is reflection-free.
/// </summary>
internal static class BrowserModelDirective
{
    /// <summary>Unboxes the directive element argument to its int node handle.</summary>
    /// <param name="element">The boxed platform node from the directive hook.</param>
    /// <exception cref="InvalidOperationException"><paramref name="element"/> is not a boxed int handle.</exception>
    public static int Handle(object? element)
        => element is int handle
            ? handle
            : throw new InvalidOperationException(
                "A DOM v-model/v-show directive ran against a non-DOM node; the element handle must be an int.");

    /// <summary>Whether a directive modifier (the <c>bar</c> in <c>v-model.bar</c>) is present.</summary>
    /// <param name="binding">The directive binding.</param>
    /// <param name="name">The modifier name.</param>
    public static bool HasModifier(DirectiveBinding binding, string name)
        => binding.Modifiers.TryGetValue(name, out var present) && present;

    /// <summary>The <see cref="ViuModelBinding"/> carried by the binding value, or null.</summary>
    /// <param name="binding">The directive binding.</param>
    public static ViuModelBinding? Carrier(DirectiveBinding binding) => binding.Value as ViuModelBinding;

    /// <summary>The model value from a binding's carrier, or null when the carrier is absent.</summary>
    /// <param name="binding">The directive binding (its <see cref="DirectiveBinding.Value"/> or <see cref="DirectiveBinding.OldValue"/>).</param>
    public static object? ModelValue(object? binding) => (binding as ViuModelBinding)?.Value;

    /// <summary>Formats a value for the DOM (null → empty; invariant scalar otherwise), matching the patch engine.</summary>
    /// <param name="value">The value to format.</param>
    public static string FormatValue(object? value)
        => value is null ? string.Empty : DisplayStringFormatter.FormatScalar(value);

    /// <summary>Reads a raw vnode property (the bound <c>:value</c>, <c>true-value</c>, …), preserving object identity.</summary>
    /// <param name="node">The element vnode.</param>
    /// <param name="name">The property name.</param>
    public static object? Property(VirtualNode node, string name) => node.Properties?[name];

    /// <summary>Whether the vnode declares <c>type="number"</c> (upstream: <c>vnode.props.type === 'number'</c>).</summary>
    /// <param name="node">The element vnode.</param>
    public static bool IsNumberType(VirtualNode node)
        => string.Equals(FormatValue(Property(node, "type")), "number", StringComparison.Ordinal);

    /// <summary>Whether a model value is an ordered list (upstream: <c>isArray</c>) — array/set checkbox and multi-select semantics branch here first.</summary>
    /// <param name="value">The model value.</param>
    public static bool IsList([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] object? value)
        => value is IList; // string does not implement IList, so no explicit exclusion is needed.

    /// <summary>
    /// Whether a model value is a set (upstream: <c>isSet</c>). Reflection-free: after
    /// <see cref="IsList(object?)"/> has been ruled out, any non-string, non-dictionary enumerable
    /// is treated as a set — matching the <c>IList</c>-or-<c>ISet</c> model contract for
    /// checkbox/select <c>v-model</c>.
    /// </summary>
    /// <param name="value">The model value (already known not to be a list).</param>
    public static bool IsSet([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] object? value)
        => value is IEnumerable and not string and not IDictionary and not IList;

    /// <summary>Copies an enumerable model collection into a fresh <see cref="List{T}"/> so a reassign triggers reactivity (upstream: <c>modelValue.concat</c> / <c>[...modelValue]</c>).</summary>
    /// <param name="values">The source collection.</param>
    public static List<object?> CopyToList(IEnumerable values)
    {
        var copy = new List<object?>();
        foreach (var item in values)
        {
            copy.Add(item);
        }
        return copy;
    }

    /// <summary>Copies an enumerable model set into a fresh <see cref="HashSet{T}"/> (upstream: <c>new Set(modelValue)</c>).</summary>
    /// <param name="values">The source set.</param>
    public static HashSet<object?> CopyToSet(IEnumerable values)
    {
        var copy = new HashSet<object?>();
        foreach (var item in values)
        {
            copy.Add(item);
        }
        return copy;
    }

    /// <summary>Whether a set contains a value by strict equality (upstream: <c>Set.has</c> — SameValueZero, not looseEqual).</summary>
    /// <param name="values">The set.</param>
    /// <param name="value">The value to test.</param>
    public static bool SetContains(IEnumerable values, object? value)
    {
        foreach (var item in values)
        {
            if (Equals(item, value))
            {
                return true;
            }
        }
        return false;
    }
}
