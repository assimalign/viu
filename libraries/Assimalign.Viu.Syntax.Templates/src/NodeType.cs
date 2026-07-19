namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// Discriminates the kinds of node the template parser produces. The C# port of the parse-relevant
/// members of Vue 3.5's <c>NodeTypes</c> (<c>@vue/compiler-core</c> <c>ast.ts</c>); the numeric values
/// match upstream for these members so downstream stages can share code-generation tables.
/// </summary>
public enum NodeType
{
    /// <summary>The template root (upstream <c>ROOT</c>).</summary>
    Root = 0,

    /// <summary>An element (upstream <c>ELEMENT</c>).</summary>
    Element = 1,

    /// <summary>A run of static text (upstream <c>TEXT</c>).</summary>
    Text = 2,

    /// <summary>An HTML comment (upstream <c>COMMENT</c>).</summary>
    Comment = 3,

    /// <summary>A simple expression (upstream <c>SIMPLE_EXPRESSION</c>).</summary>
    SimpleExpression = 4,

    /// <summary>A mustache interpolation such as <c>{{ value }}</c> (upstream <c>INTERPOLATION</c>).</summary>
    Interpolation = 5,

    /// <summary>A plain attribute (upstream <c>ATTRIBUTE</c>).</summary>
    Attribute = 6,

    /// <summary>A directive such as <c>v-if</c> or <c>:class</c> (upstream <c>DIRECTIVE</c>).</summary>
    Directive = 7,

    // ---- container types introduced by the transform stage ([V01.01.05.02]/[V01.01.05.03]) ----

    /// <summary>A compound expression built from concatenated parts (upstream <c>COMPOUND_EXPRESSION</c>).</summary>
    CompoundExpression = 8,

    /// <summary>A grouped <c>v-if</c>/<c>v-else-if</c>/<c>v-else</c> chain (upstream <c>IF</c>).</summary>
    If = 9,

    /// <summary>A single branch of an <see cref="If"/> chain (upstream <c>IF_BRANCH</c>).</summary>
    IfBranch = 10,

    /// <summary>A <c>v-for</c> fragment block (upstream <c>FOR</c>).</summary>
    For = 11,

    /// <summary>A pre-converted text vnode call (upstream <c>TEXT_CALL</c>).</summary>
    TextCall = 12,

    // ---- code-generation node types (upstream's minimal JS AST subset) ----

    /// <summary>A <c>createVNode</c>/<c>createElementBlock</c> vnode call (upstream <c>VNODE_CALL</c>).</summary>
    VNodeCall = 13,

    /// <summary>A JavaScript call expression (upstream <c>JS_CALL_EXPRESSION</c>).</summary>
    JsCallExpression = 14,

    /// <summary>A JavaScript object literal (upstream <c>JS_OBJECT_EXPRESSION</c>).</summary>
    JsObjectExpression = 15,

    /// <summary>A JavaScript object property (upstream <c>JS_PROPERTY</c>).</summary>
    JsProperty = 16,

    /// <summary>A JavaScript array literal (upstream <c>JS_ARRAY_EXPRESSION</c>).</summary>
    JsArrayExpression = 17,

    /// <summary>A JavaScript function expression (upstream <c>JS_FUNCTION_EXPRESSION</c>).</summary>
    JsFunctionExpression = 18,

    /// <summary>A JavaScript conditional (ternary) expression (upstream <c>JS_CONDITIONAL_EXPRESSION</c>).</summary>
    JsConditionalExpression = 19,

    /// <summary>A JavaScript cache-slot expression for <c>v-once</c>/cached handlers (upstream <c>JS_CACHE_EXPRESSION</c>).</summary>
    JsCacheExpression = 20,

    /// <summary>A JavaScript block statement, the body of a memoized <c>v-for</c> loop (upstream <c>JS_BLOCK_STATEMENT</c>).</summary>
    JsBlockStatement = 21,
}
