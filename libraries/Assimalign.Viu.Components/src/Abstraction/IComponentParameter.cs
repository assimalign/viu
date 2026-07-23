using System;

namespace Assimalign.Viu.Components;

/// <summary>Describes one declared component input parameter.</summary>
/// <remarks>
/// This is Viu's AOT-safe equivalent of Vue's runtime
/// <see href="https://vuejs.org/guide/components/props.html#prop-validation">prop declaration</see>.
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
