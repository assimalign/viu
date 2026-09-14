using System;

namespace Assimalign.Viu.Browser;

/// <summary>
/// Configures a custom-element definition. Values are snapshotted by definition; this mutable
/// configuration is not thread-safe. Specified by <c>[CEL-1]</c>, <c>[CEL-3]</c>, and <c>[CEL-6]</c>.
/// </summary>
public sealed class CustomElementOptions
{
    /// <summary>Gets or sets whether instances render into an open shadow root; defaults to true.</summary>
    public bool UseShadowRoot { get; set; } = true;

    /// <summary>
    /// Gets or sets an optional mapping from canonical parameter names to lowercase attribute names.
    /// Null selects kebab-case; an empty result disables attribute reflection for that parameter.
    /// Returning null is invalid and causes definition to fail before a native class is registered.
    /// Mapping is evaluated once per definition and never changes JavaScript property names.
    /// Specified by <c>[CEL-3]</c> and <c>[CEL-4]</c>.
    /// </summary>
    public Func<string, string>? AttributeNameMapper { get; set; }
}
