using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>Provides an immutable, ordered collection of component attributes.</summary>
public interface IComponentAttributeCollection : IReadOnlyList<IComponentAttribute>
{
    /// <summary>Attempts to read an attribute by its ordinal name.</summary>
    /// <param name="name">The attribute name.</param>
    /// <param name="value">The attribute value when present.</param>
    /// <returns>True when the name is present.</returns>
    bool TryGetValue(string name, out object? value);
}

