namespace Assimalign.Vue.Sfc;

/// <summary>
/// A point in a <c>.viu</c> single-file component source: a zero-based character offset plus its
/// one-based line and column. Mirrors the shape of Vue 3.5's <c>Position</c> interface
/// (<c>@vue/compiler-core</c> <c>ast.ts</c>, re-exported by <c>@vue/compiler-sfc</c>) so a block's spans
/// line up with the template compiler's own locations. A value type with structural equality so it
/// participates in the descriptor's value comparison (the incremental-caching contract of
/// [V01.01.06.01]).
/// </summary>
/// <param name="Offset">Zero-based character offset from the start of the file.</param>
/// <param name="Line">One-based line number.</param>
/// <param name="Column">One-based column number.</param>
public readonly record struct Position(int Offset, int Line, int Column);
