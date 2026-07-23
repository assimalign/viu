using System;

namespace Assimalign.Viu.Components;

/// <summary>Controls reusable template behavior.</summary>
[Flags]
public enum ComponentFlags
{
    /// <summary>No optional behavior is enabled.</summary>
    None = 0,

    /// <summary>Undeclared attributes fall through to a single element root.</summary>
    InheritAttributes = 1,
}

