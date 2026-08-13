namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The static-ness level of an expression, used by later pipeline stages (hoisting, patch-flag
/// elision) to decide how aggressively a node can be cached. The levels are ordered and cumulative —
/// higher levels imply the lower ones — so a stage compares with <c>&gt;=</c> rather than testing for
/// an exact level. The numeric values are therefore load-bearing and additive only.
/// </summary>
public enum ConstantType
{
    /// <summary>Not constant; must be evaluated on every render.</summary>
    NotConstant = 0,

    /// <summary>Constant enough to skip patching.</summary>
    CanSkipPatch = 1,

    /// <summary>Constant enough to cache.</summary>
    CanCache = 2,

    /// <summary>Constant enough to stringify.</summary>
    CanStringify = 3,
}
