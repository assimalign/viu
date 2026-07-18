namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// The static-ness level of an expression, used by later pipeline stages (hoisting, patch-flag
/// elision) to decide how aggressively a node can be cached. The C# port of Vue 3.5's
/// <c>ConstantTypes</c> enum (<c>@vue/compiler-core</c> <c>ast.ts</c>); numeric values match upstream.
/// Higher levels imply the lower ones.
/// </summary>
public enum ConstantType
{
    /// <summary>Not constant; must be evaluated on every render (upstream <c>NOT_CONSTANT</c>).</summary>
    NotConstant = 0,

    /// <summary>Constant enough to skip patching (upstream <c>CAN_SKIP_PATCH</c>).</summary>
    CanSkipPatch = 1,

    /// <summary>Constant enough to cache (upstream <c>CAN_CACHE</c>).</summary>
    CanCache = 2,

    /// <summary>Constant enough to stringify (upstream <c>CAN_STRINGIFY</c>).</summary>
    CanStringify = 3,
}
