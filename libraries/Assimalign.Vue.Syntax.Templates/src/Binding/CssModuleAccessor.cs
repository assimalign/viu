using System;
using System.Collections.Generic;

namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// One CSS Modules accessor a template expression may reference — the Vuecs equivalent of Vue 3.5's
/// <c>$style</c> (and named-module) render-context object (https://vuejs.org/api/sfc-css-features.html#css-modules).
/// Vue exposes the class map as a runtime object indexed dynamically; Vuecs has no such render-context object, so
/// the composition-root generator ([V01.01.06.06]) emits the map as a compile-time nested <c>const</c> class and
/// supplies this descriptor to expression classification ([V01.01.05.04.01]) so <c>$style.box</c> resolves to that
/// class's <c>box</c> member instead of a phantom component binding.
/// </summary>
/// <remarks>
/// This is transform <i>input</i>, like <see cref="BindingMetadata"/>: a plain immutable class (not a value-
/// equatable record) rebuilt from the generator's already-value-equatable module-class map, so it never rides in
/// the cached model and cannot perturb the incremental cache.
/// </remarks>
public sealed class CssModuleAccessor
{
    private readonly HashSet<string> members;

    /// <summary>Creates a CSS module accessor descriptor.</summary>
    /// <param name="templateName">The accessor as authored in the template (<c>$style</c>, or a named module's name).</param>
    /// <param name="parseIdentifier">
    /// The C#-parseable spelling of <paramref name="templateName"/> — identical for a named module, and the
    /// <c>$</c>→<c>_</c> substitution for the default <c>$style</c> (<c>$</c> is not a legal C# identifier
    /// character, so the expression is parsed against <c>_style</c>). Length-preserving, so expression offsets
    /// map back to the template unchanged.
    /// </param>
    /// <param name="accessorClass">The generated accessor class name the member access compiles against (<c>Style</c>, <c>Theme</c>).</param>
    /// <param name="members">The declared member names (the sanitized class names the accessor class exposes as <c>const</c>s).</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public CssModuleAccessor(string templateName, string parseIdentifier, string accessorClass, IEnumerable<string> members)
    {
        TemplateName = templateName ?? throw new ArgumentNullException(nameof(templateName));
        ParseIdentifier = parseIdentifier ?? throw new ArgumentNullException(nameof(parseIdentifier));
        AccessorClass = accessorClass ?? throw new ArgumentNullException(nameof(accessorClass));
        this.members = new HashSet<string>(members ?? throw new ArgumentNullException(nameof(members)), StringComparer.Ordinal);
    }

    /// <summary>The accessor as authored in the template (<c>$style</c>, or a named module's name).</summary>
    public string TemplateName { get; }

    /// <summary>The C#-parseable spelling of <see cref="TemplateName"/> (the <c>$</c>→<c>_</c> form for <c>$style</c>).</summary>
    public string ParseIdentifier { get; }

    /// <summary>The generated accessor class name a member access compiles against.</summary>
    public string AccessorClass { get; }

    /// <summary>The declared member names.</summary>
    public IReadOnlyCollection<string> Members => members;

    /// <summary>Whether <paramref name="name"/> is a declared member of this accessor.</summary>
    /// <param name="name">The member name.</param>
    public bool HasMember(string name) => members.Contains(name);
}
