namespace Assimalign.Vue.Compiler;

/// <summary>
/// A point in the template source. The C# port of Vue 3.5's <c>Position</c> interface
/// (<c>@vue/compiler-core</c> <c>ast.ts</c>). A value type with structural equality so it participates
/// in the AST's value comparison (the incremental-caching contract of [V01.01.05.01]).
/// </summary>
/// <param name="Offset">Zero-based character offset from the start of the template.</param>
/// <param name="Line">One-based line number.</param>
/// <param name="Column">One-based column number.</param>
public readonly record struct Position(int Offset, int Line, int Column);
