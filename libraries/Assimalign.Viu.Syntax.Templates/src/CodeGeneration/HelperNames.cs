namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The canonical table of runtime helper references the transform pipeline can emit. Each field is a
/// <see cref="RuntimeHelper"/> whose <see cref="RuntimeHelper.Name"/> equals the member name generated
/// code binds against on <c>Assimalign.Viu.RenderHelpers</c> (and <c>DomRenderHelpers</c> for the DOM
/// helpers below). The binding is <b>by name</b>: no <c>Assimalign.Viu.Syntax.*</c> assembly references
/// any runtime assembly, so the name in this table is a one-way contract that a rename on either side
/// breaks (<c>[SFC-CG-2]</c>).
/// </summary>
public static class HelperNames
{
    /// <summary>The <c>Fragment</c> block type.</summary>
    public static readonly RuntimeHelper Fragment = new("Fragment");

    /// <summary>The <c>Teleport</c> built-in.</summary>
    public static readonly RuntimeHelper Teleport = new("Teleport");

    /// <summary>The <c>Suspense</c> built-in.</summary>
    public static readonly RuntimeHelper Suspense = new("Suspense");

    /// <summary>The <c>KeepAlive</c> built-in.</summary>
    public static readonly RuntimeHelper KeepAlive = new("KeepAlive");

    /// <summary>The <c>BaseTransition</c> built-in.</summary>
    public static readonly RuntimeHelper BaseTransition = new("BaseTransition");

    /// <summary>Opens an optimization block.</summary>
    public static readonly RuntimeHelper OpenBlock = new("openBlock");

    /// <summary>Creates a component block vnode.</summary>
    public static readonly RuntimeHelper CreateBlock = new("createBlock");

    /// <summary>Creates an element block vnode.</summary>
    public static readonly RuntimeHelper CreateElementBlock = new("createElementBlock");

    /// <summary>Creates a component vnode.</summary>
    public static readonly RuntimeHelper CreateVNode = new("createVNode");

    /// <summary>Creates an element vnode.</summary>
    public static readonly RuntimeHelper CreateElementVNode = new("createElementVNode");

    /// <summary>Creates a comment vnode.</summary>
    public static readonly RuntimeHelper CreateComment = new("createCommentVNode");

    /// <summary>Creates a text vnode.</summary>
    public static readonly RuntimeHelper CreateText = new("createTextVNode");

    /// <summary>Creates a static vnode.</summary>
    public static readonly RuntimeHelper CreateStatic = new("createStaticVNode");

    /// <summary>Resolves a component by name.</summary>
    public static readonly RuntimeHelper ResolveComponent = new("resolveComponent");

    /// <summary>Resolves a dynamic component.</summary>
    public static readonly RuntimeHelper ResolveDynamicComponent = new("resolveDynamicComponent");

    /// <summary>Resolves a directive by name.</summary>
    public static readonly RuntimeHelper ResolveDirective = new("resolveDirective");

    /// <summary>Resolves a filter by name.</summary>
    public static readonly RuntimeHelper ResolveFilter = new("resolveFilter");

    /// <summary>Applies runtime directives to a vnode.</summary>
    public static readonly RuntimeHelper WithDirectives = new("withDirectives");

    /// <summary>Renders a list for <c>v-for</c>.</summary>
    public static readonly RuntimeHelper RenderList = new("renderList");

    /// <summary>Renders a slot outlet.</summary>
    public static readonly RuntimeHelper RenderSlot = new("renderSlot");

    /// <summary>Creates a dynamic slots object.</summary>
    public static readonly RuntimeHelper CreateSlots = new("createSlots");

    /// <summary>Stringifies an interpolation value.</summary>
    public static readonly RuntimeHelper ToDisplayString = new("toDisplayString");

    /// <summary>Merges multiple props sources.</summary>
    public static readonly RuntimeHelper MergeProps = new("mergeProps");

    /// <summary>Normalizes a dynamic <c>class</c> binding.</summary>
    public static readonly RuntimeHelper NormalizeClass = new("normalizeClass");

    /// <summary>Normalizes a dynamic <c>style</c> binding.</summary>
    public static readonly RuntimeHelper NormalizeStyle = new("normalizeStyle");

    /// <summary>Normalizes a props object with dynamic keys.</summary>
    public static readonly RuntimeHelper NormalizeProps = new("normalizeProps");

    /// <summary>Guards a reactive props object.</summary>
    public static readonly RuntimeHelper GuardReactiveProps = new("guardReactiveProps");

    /// <summary>Normalizes a <c>v-on="object"</c> handlers map.</summary>
    public static readonly RuntimeHelper ToHandlers = new("toHandlers");

    /// <summary>Camel-cases a dynamic argument.</summary>
    public static readonly RuntimeHelper Camelize = new("camelize");

    /// <summary>Capitalizes a string.</summary>
    public static readonly RuntimeHelper Capitalize = new("capitalize");

    /// <summary>Builds an <c>onXxx</c> handler key.</summary>
    public static readonly RuntimeHelper ToHandlerKey = new("toHandlerKey");

    /// <summary>Toggles block tracking for <c>v-once</c>.</summary>
    public static readonly RuntimeHelper SetBlockTracking = new("setBlockTracking");

    /// <summary>Wraps a slot function with the render context.</summary>
    public static readonly RuntimeHelper WithCtx = new("withCtx");

    /// <summary>Unwraps a ref.</summary>
    public static readonly RuntimeHelper Unref = new("unref");

    /// <summary>Tests whether a value is a ref.</summary>
    public static readonly RuntimeHelper IsRef = new("isRef");

    /// <summary>Memoizes a subtree for <c>v-memo</c>.</summary>
    public static readonly RuntimeHelper WithMemo = new("withMemo");

    /// <summary>Compares two memo dependency arrays.</summary>
    public static readonly RuntimeHelper IsMemoSame = new("isMemoSame");

    // ---- DOM helpers: bound against Assimalign.Viu.Browser.DomRenderHelpers ----

    /// <summary>The <c>v-model</c> directive for radio inputs.</summary>
    public static readonly RuntimeHelper VModelRadio = new("vModelRadio");

    /// <summary>The <c>v-model</c> directive for checkbox inputs.</summary>
    public static readonly RuntimeHelper VModelCheckbox = new("vModelCheckbox");

    /// <summary>The <c>v-model</c> directive for text inputs.</summary>
    public static readonly RuntimeHelper VModelText = new("vModelText");

    /// <summary>The <c>v-model</c> directive for select elements.</summary>
    public static readonly RuntimeHelper VModelSelect = new("vModelSelect");

    /// <summary>The <c>v-model</c> directive for dynamic input types.</summary>
    public static readonly RuntimeHelper VModelDynamic = new("vModelDynamic");

    /// <summary>The <c>v-on</c> modifier guard wrapper.</summary>
    public static readonly RuntimeHelper WithModifiers = new("withModifiers");

    /// <summary>The <c>v-on</c> key guard wrapper.</summary>
    public static readonly RuntimeHelper WithKeys = new("withKeys");

    /// <summary>The <c>v-show</c> directive.</summary>
    public static readonly RuntimeHelper VShow = new("vShow");

    /// <summary>The <c>Transition</c> DOM built-in.</summary>
    public static readonly RuntimeHelper Transition = new("Transition");

    /// <summary>The <c>TransitionGroup</c> DOM built-in.</summary>
    public static readonly RuntimeHelper TransitionGroup = new("TransitionGroup");
}
