namespace Assimalign.Vue.Compiler;

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
}
