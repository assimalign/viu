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
}
