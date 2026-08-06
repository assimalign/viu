namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// Classifies an <see cref="ElementNode"/> once its close tag is seen. The classification decides which
/// code-generation path an element takes, so it is refined during parsing rather than re-derived later.
/// </summary>
public enum ElementType
{
    /// <summary>A native/platform element.</summary>
    Element = 0,

    /// <summary>A component invocation.</summary>
    Component = 1,

    /// <summary>A <c>&lt;slot&gt;</c> outlet.</summary>
    Slot = 2,

    /// <summary>A <c>&lt;template&gt;</c> container carrying a structural directive.</summary>
    Template = 3,
}
