namespace Assimalign.Vue.Compiler;

/// <summary>
/// The base of every template AST node: an immutable, value-comparable record carrying the node's
/// <see cref="SourceLocation"/>. The C# port of Vue 3.5's <c>Node</c> interface
/// (<c>@vue/compiler-core</c> <c>ast.ts</c>). Records give the whole tree structural equality, which is
/// the incremental-caching contract of [V01.01.05.01]: parsing equal input twice yields equal ASTs.
/// </summary>
public abstract record SyntaxNode
{
    /// <summary>The source range this node spans, with the exact original slice.</summary>
    public required SourceLocation Location { get; init; }

    /// <summary>The node kind discriminator.</summary>
    public abstract NodeType NodeType { get; }
}


/*
    Assimalign.Vue.Syntax
    Assimalign.Vue.Syntax.Compiler
    Assimalign.Vue.Syntax.SingleFileComponent


public abstract class SyntaxNode {

}

public abstract class SyntaxParser 
{
    
}
 
 */