namespace Assimalign.Vue.Syntax.Compiler;

/// <summary>
/// The canonical table of runtime helper references the transform pipeline can emit. The C# port of the
/// <c>helperNameMap</c> in Vue 3.5's <c>@vue/compiler-core</c> <c>runtimeHelpers.ts</c> plus the DOM helpers
/// registered by <c>@vue/compiler-dom</c> <c>runtimeHelpers.ts</c>. Each field is a <see cref="RuntimeHelper"/>
/// whose <see cref="RuntimeHelper.Name"/> equals the runtime export name; code generation ([V01.01.05.05])
/// binds these names to the concrete runtime symbols. The compiler itself never references the runtime.
/// </summary>
public static class HelperNames
{
    /// <summary>The <c>Fragment</c> block type (upstream <c>FRAGMENT</c>).</summary>
    public static readonly RuntimeHelper Fragment = new("Fragment");

    /// <summary>The <c>Teleport</c> built-in (upstream <c>TELEPORT</c>).</summary>
    public static readonly RuntimeHelper Teleport = new("Teleport");

    /// <summary>The <c>Suspense</c> built-in (upstream <c>SUSPENSE</c>).</summary>
    public static readonly RuntimeHelper Suspense = new("Suspense");

    /// <summary>The <c>KeepAlive</c> built-in (upstream <c>KEEP_ALIVE</c>).</summary>
    public static readonly RuntimeHelper KeepAlive = new("KeepAlive");

    /// <summary>The <c>BaseTransition</c> built-in (upstream <c>BASE_TRANSITION</c>).</summary>
    public static readonly RuntimeHelper BaseTransition = new("BaseTransition");

    /// <summary>Opens an optimization block (upstream <c>OPEN_BLOCK</c>).</summary>
    public static readonly RuntimeHelper OpenBlock = new("openBlock");

    /// <summary>Creates a component block vnode (upstream <c>CREATE_BLOCK</c>).</summary>
    public static readonly RuntimeHelper CreateBlock = new("createBlock");

    /// <summary>Creates an element block vnode (upstream <c>CREATE_ELEMENT_BLOCK</c>).</summary>
    public static readonly RuntimeHelper CreateElementBlock = new("createElementBlock");

    /// <summary>Creates a component vnode (upstream <c>CREATE_VNODE</c>).</summary>
    public static readonly RuntimeHelper CreateVNode = new("createVNode");

    /// <summary>Creates an element vnode (upstream <c>CREATE_ELEMENT_VNODE</c>).</summary>
    public static readonly RuntimeHelper CreateElementVNode = new("createElementVNode");

    /// <summary>Creates a comment vnode (upstream <c>CREATE_COMMENT</c>).</summary>
    public static readonly RuntimeHelper CreateComment = new("createCommentVNode");

    /// <summary>Creates a text vnode (upstream <c>CREATE_TEXT</c>).</summary>
    public static readonly RuntimeHelper CreateText = new("createTextVNode");

    /// <summary>Creates a static vnode (upstream <c>CREATE_STATIC</c>).</summary>
    public static readonly RuntimeHelper CreateStatic = new("createStaticVNode");

    /// <summary>Resolves a component by name (upstream <c>RESOLVE_COMPONENT</c>).</summary>
    public static readonly RuntimeHelper ResolveComponent = new("resolveComponent");

    /// <summary>Resolves a dynamic component (upstream <c>RESOLVE_DYNAMIC_COMPONENT</c>).</summary>
    public static readonly RuntimeHelper ResolveDynamicComponent = new("resolveDynamicComponent");

    /// <summary>Resolves a directive by name (upstream <c>RESOLVE_DIRECTIVE</c>).</summary>
    public static readonly RuntimeHelper ResolveDirective = new("resolveDirective");

    /// <summary>Resolves a filter by name (upstream <c>RESOLVE_FILTER</c>).</summary>
    public static readonly RuntimeHelper ResolveFilter = new("resolveFilter");

    /// <summary>Applies runtime directives to a vnode (upstream <c>WITH_DIRECTIVES</c>).</summary>
    public static readonly RuntimeHelper WithDirectives = new("withDirectives");

    /// <summary>Renders a list for <c>v-for</c> (upstream <c>RENDER_LIST</c>).</summary>
    public static readonly RuntimeHelper RenderList = new("renderList");

    /// <summary>Renders a slot outlet (upstream <c>RENDER_SLOT</c>).</summary>
    public static readonly RuntimeHelper RenderSlot = new("renderSlot");

    /// <summary>Creates a dynamic slots object (upstream <c>CREATE_SLOTS</c>).</summary>
    public static readonly RuntimeHelper CreateSlots = new("createSlots");

    /// <summary>Stringifies an interpolation value (upstream <c>TO_DISPLAY_STRING</c>).</summary>
    public static readonly RuntimeHelper ToDisplayString = new("toDisplayString");

    /// <summary>Merges multiple props sources (upstream <c>MERGE_PROPS</c>).</summary>
    public static readonly RuntimeHelper MergeProps = new("mergeProps");

    /// <summary>Normalizes a dynamic <c>class</c> binding (upstream <c>NORMALIZE_CLASS</c>).</summary>
    public static readonly RuntimeHelper NormalizeClass = new("normalizeClass");

    /// <summary>Normalizes a dynamic <c>style</c> binding (upstream <c>NORMALIZE_STYLE</c>).</summary>
    public static readonly RuntimeHelper NormalizeStyle = new("normalizeStyle");

    /// <summary>Normalizes a props object with dynamic keys (upstream <c>NORMALIZE_PROPS</c>).</summary>
    public static readonly RuntimeHelper NormalizeProps = new("normalizeProps");

    /// <summary>Guards a reactive props object (upstream <c>GUARD_REACTIVE_PROPS</c>).</summary>
    public static readonly RuntimeHelper GuardReactiveProps = new("guardReactiveProps");

    /// <summary>Normalizes a <c>v-on="object"</c> handlers map (upstream <c>TO_HANDLERS</c>).</summary>
    public static readonly RuntimeHelper ToHandlers = new("toHandlers");

    /// <summary>Camel-cases a dynamic argument (upstream <c>CAMELIZE</c>).</summary>
    public static readonly RuntimeHelper Camelize = new("camelize");

    /// <summary>Capitalizes a string (upstream <c>CAPITALIZE</c>).</summary>
    public static readonly RuntimeHelper Capitalize = new("capitalize");

    /// <summary>Builds an <c>onXxx</c> handler key (upstream <c>TO_HANDLER_KEY</c>).</summary>
    public static readonly RuntimeHelper ToHandlerKey = new("toHandlerKey");

    /// <summary>Toggles block tracking for <c>v-once</c> (upstream <c>SET_BLOCK_TRACKING</c>).</summary>
    public static readonly RuntimeHelper SetBlockTracking = new("setBlockTracking");

    /// <summary>Wraps a slot function with the render context (upstream <c>WITH_CTX</c>).</summary>
    public static readonly RuntimeHelper WithCtx = new("withCtx");

    /// <summary>Unwraps a ref (upstream <c>UNREF</c>).</summary>
    public static readonly RuntimeHelper Unref = new("unref");

    /// <summary>Tests whether a value is a ref (upstream <c>IS_REF</c>).</summary>
    public static readonly RuntimeHelper IsRef = new("isRef");

    /// <summary>Memoizes a subtree for <c>v-memo</c> (upstream <c>WITH_MEMO</c>).</summary>
    public static readonly RuntimeHelper WithMemo = new("withMemo");

    /// <summary>Compares two memo dependency arrays (upstream <c>IS_MEMO_SAME</c>).</summary>
    public static readonly RuntimeHelper IsMemoSame = new("isMemoSame");

    // ---- DOM helpers (upstream @vue/compiler-dom runtimeHelpers.ts) ----

    /// <summary>The <c>v-model</c> directive for radio inputs (upstream <c>V_MODEL_RADIO</c>).</summary>
    public static readonly RuntimeHelper VModelRadio = new("vModelRadio");

    /// <summary>The <c>v-model</c> directive for checkbox inputs (upstream <c>V_MODEL_CHECKBOX</c>).</summary>
    public static readonly RuntimeHelper VModelCheckbox = new("vModelCheckbox");

    /// <summary>The <c>v-model</c> directive for text inputs (upstream <c>V_MODEL_TEXT</c>).</summary>
    public static readonly RuntimeHelper VModelText = new("vModelText");

    /// <summary>The <c>v-model</c> directive for select elements (upstream <c>V_MODEL_SELECT</c>).</summary>
    public static readonly RuntimeHelper VModelSelect = new("vModelSelect");

    /// <summary>The <c>v-model</c> directive for dynamic input types (upstream <c>V_MODEL_DYNAMIC</c>).</summary>
    public static readonly RuntimeHelper VModelDynamic = new("vModelDynamic");

    /// <summary>The <c>v-on</c> modifier guard wrapper (upstream <c>V_ON_WITH_MODIFIERS</c>).</summary>
    public static readonly RuntimeHelper WithModifiers = new("withModifiers");

    /// <summary>The <c>v-on</c> key guard wrapper (upstream <c>V_ON_WITH_KEYS</c>).</summary>
    public static readonly RuntimeHelper WithKeys = new("withKeys");

    /// <summary>The <c>v-show</c> directive (upstream <c>V_SHOW</c>).</summary>
    public static readonly RuntimeHelper VShow = new("vShow");

    /// <summary>The <c>Transition</c> DOM built-in (upstream <c>TRANSITION</c>).</summary>
    public static readonly RuntimeHelper Transition = new("Transition");

    /// <summary>The <c>TransitionGroup</c> DOM built-in (upstream <c>TRANSITION_GROUP</c>).</summary>
    public static readonly RuntimeHelper TransitionGroup = new("TransitionGroup");
}
