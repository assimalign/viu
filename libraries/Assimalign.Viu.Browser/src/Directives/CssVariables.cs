using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

/// <summary>Applies generated CSS custom-property values to one bound Browser element.</summary>
/// <remarks>
/// The qualified type is both the compiler-known directive token and its reusable implementation.
/// All custom properties are sent through one batched host operation. This element-local form uses
/// only the public directive lifecycle and does not discover component roots. Specified by
/// <c>[CMP-7]</c> and <c>[RND-IO-2]</c>.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class CssVariables : IDirective
{
    /// <summary>Gets the reusable Browser CSS-variable directive.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CssVariables Instance { get; } = new();

    private CssVariables()
    {
    }

    /// <summary>Creates an immutable directive invocation from generated variable values.</summary>
    /// <param name="variables">
    /// Custom-property names without the leading <c>--</c>, paired with their formatted values.
    /// </param>
    /// <returns>A compiler-ready invocation carrying a copied ordinal snapshot.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static DirectiveInvocation Bind(
        IReadOnlyDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        Dictionary<string, string> snapshot = new(variables.Count, StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> variable in variables)
        {
            ArgumentException.ThrowIfNullOrEmpty(variable.Key);
            ArgumentNullException.ThrowIfNull(variable.Value);
            snapshot[variable.Key] = variable.Value;
        }

        return new DirectiveInvocation(
            typeof(CssVariables),
            new ReadOnlyDictionary<string, string>(snapshot));
    }

    /// <inheritdoc/>
    public DirectiveHook? BeforeMount => Apply;

    /// <inheritdoc/>
    public DirectiveHook? BeforeUpdate => Apply;

    private static void Apply(
        object element,
        DirectiveBinding binding,
        ElementNode value,
        ElementNode? previousValue)
    {
        if (binding.Value is not IReadOnlyDictionary<string, string> variables
            || variables.Count == 0)
        {
            return;
        }

        string[] names = new string[variables.Count];
        string[] values = new string[variables.Count];
        int index = 0;
        foreach (KeyValuePair<string, string> variable in variables)
        {
            names[index] = variable.Key.StartsWith("--", StringComparison.Ordinal)
                ? variable.Key
                : "--" + variable.Key;
            values[index] = variable.Value;
            index++;
        }

        BrowserDirectiveOperations.Require().SetCssVariables(
            BrowserModelDirective.Handle(element),
            names,
            values);
    }
}
