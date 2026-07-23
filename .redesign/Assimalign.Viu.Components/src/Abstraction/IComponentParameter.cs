using System;

namespace Assimalign.Viu.Components;

/// <summary>Describes one declared component input parameter.</summary>
public interface IComponentParameter
{
    /// <summary>Gets the parameter name.</summary>
    string Name { get; }

    /// <summary>Gets whether the caller must supply the parameter.</summary>
    bool IsRequired { get; }

    /// <summary>Gets the optional factory that produces a default value per mount.</summary>
    Func<object?>? DefaultFactory { get; }
}

