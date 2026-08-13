using System.ComponentModel;

namespace Assimalign.Viu;

/// <summary>Classifies one compiler-registered component metadata update.</summary>
/// <remarks>
/// Public only for the development runtime and generated-code contract; application code does not
/// branch on this value. Specified by <c>[SFC-CG-2]</c> and <c>[SFC-CG-4]</c>.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public enum ComponentHotReloadChangeKind
{
    /// <summary>No registered component marker changed.</summary>
    None,

    /// <summary>Only stylesheet output changed; Core performs no component work.</summary>
    StyleOnly,

    /// <summary>Generated template code changed and mounted instances must remount.</summary>
    Template,

    /// <summary>Generated script code changed and mounted instances must reset.</summary>
    ScriptReset,
}
