namespace Assimalign.Vue.Compiler;

/// <summary>
/// Classifies an <see cref="ElementNode"/> once its close tag is seen. The C# port of Vue 3.5's
/// <c>ElementTypes</c> enum (<c>@vue/compiler-core</c> <c>ast.ts</c>); numeric values match upstream.
/// </summary>
public enum ElementType
{
    /// <summary>A native/platform element (upstream <c>ELEMENT</c>).</summary>
    Element = 0,

    /// <summary>A component invocation (upstream <c>COMPONENT</c>).</summary>
    Component = 1,

    /// <summary>A <c>&lt;slot&gt;</c> outlet (upstream <c>SLOT</c>).</summary>
    Slot = 2,

    /// <summary>A <c>&lt;template&gt;</c> container carrying a structural directive (upstream <c>TEMPLATE</c>).</summary>
    Template = 3,
}
