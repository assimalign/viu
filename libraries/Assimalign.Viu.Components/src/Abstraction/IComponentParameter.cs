using System;

namespace Assimalign.Viu.Components;

/// <summary>Describes one declared component input parameter.</summary>
/// <remarks>
/// A declaration is a static value rather than reflection over the template type, so a component's
/// inputs — and their defaults and validators — are known without runtime type inspection, which is
/// what keeps argument resolution trimming- and AOT-safe. <see cref="Name"/> is the canonical key in
/// <c>IComponentContext.Arguments</c>; a parent may spell it camel-case or kebab-case and both
/// resolve to it. Specified by <c>[CMP-12]</c> and <c>[CMP-13]</c>.
/// </remarks>
public interface IComponentParameter
{
    /// <summary>Gets the parameter name.</summary>
    string Name { get; }

    /// <summary>Gets whether the caller must supply the parameter.</summary>
    bool IsRequired { get; }

    /// <summary>Gets the optional factory that produces a default value per mount.</summary>
    Func<object?>? DefaultFactory { get; }

    /// <summary>
    /// Gets the optional validator. Returning <see langword="false"/> produces a development
    /// warning without rejecting the supplied value.
    /// </summary>
    Func<object?, bool>? Validator { get; }

    /// <summary>
    /// Gets the parameter's declared value type, or <see langword="null"/> when the declaration does
    /// not state one.
    /// </summary>
    /// <remarks>
    /// The type is **descriptive**: Core never converts a supplied argument to it, because argument
    /// resolution stays a dictionary lookup with no reflection [CMP-12]. It exists so a declaration
    /// carries the same information in metadata that it carries in source, which is what lets a
    /// consumer's template be type-checked at build time ([SFC-USE-2]). Nullable reference
    /// annotations are absent — they are not part of a runtime type — while
    /// <see cref="System.Nullable{T}"/> is preserved. Default-implemented as
    /// <see langword="null"/> so an existing implementor keeps compiling unchanged.
    /// </remarks>
    Type? ParameterType => null;
}
