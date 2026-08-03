namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// Discriminates the kinds of node the template parser produces. The numeric values are a frozen
/// catalog: the parse, transform, and code-generation stages key their dispatch tables on them, and
/// <see cref="TemplateSyntaxNode.RawKind"/> projects them for language-agnostic infrastructure, so a
/// value may be added but never renumbered or reused.
/// </summary>
public enum NodeType
{
    /// <summary>The template root.</summary>
    Root = 0,

    /// <summary>An element.</summary>
    Element = 1,

    /// <summary>A run of static text.</summary>
    Text = 2,

    /// <summary>An HTML comment.</summary>
    Comment = 3,

    /// <summary>A simple expression.</summary>
    SimpleExpression = 4,

    /// <summary>A mustache interpolation such as <c>{{ value }}</c>.</summary>
    Interpolation = 5,

    /// <summary>A plain attribute.</summary>
    Attribute = 6,

    /// <summary>A directive such as <c>v-if</c> or <c>:class</c>.</summary>
    Directive = 7,

    // ---- container types introduced by the transform stage ([V01.01.05.02]/[V01.01.05.03]) ----

    /// <summary>A compound expression built from concatenated parts.</summary>
    CompoundExpression = 8,

    /// <summary>A grouped <c>v-if</c>/<c>v-else-if</c>/<c>v-else</c> chain.</summary>
    If = 9,

    /// <summary>A single branch of an <see cref="If"/> chain.</summary>
    IfBranch = 10,

    /// <summary>A <c>v-for</c> fragment block.</summary>
    For = 11,

    /// <summary>A pre-converted text render-node call.</summary>
    TextCall = 12,

    // ---- code-generation node types: the minimal expression subset the emitter needs ----

    /// <summary>A render-node construction call.</summary>
    VNodeCall = 13,

    /// <summary>A call expression.</summary>
    JsCallExpression = 14,

    /// <summary>An object literal.</summary>
    JsObjectExpression = 15,

    /// <summary>An object property.</summary>
    JsProperty = 16,

    /// <summary>An array literal.</summary>
    JsArrayExpression = 17,

    /// <summary>A function expression.</summary>
    JsFunctionExpression = 18,

    /// <summary>A conditional (ternary) expression.</summary>
    JsConditionalExpression = 19,

    /// <summary>A cache-slot expression for <c>v-once</c>/cached handlers.</summary>
    JsCacheExpression = 20,

    /// <summary>A block statement, the body of a memoized <c>v-for</c> loop.</summary>
    JsBlockStatement = 21,
}
