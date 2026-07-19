namespace Assimalign.Viu.Syntax;

/// <summary>
/// A point in a parsed source string. The C# port of Vue 3.5's <c>Position</c> interface
/// (<c>@vue/compiler-core</c> <c>ast.ts</c>, re-exported by <c>@vue/compiler-sfc</c>). Shared by every
/// <c>Assimalign.Viu.Syntax.*</c> parser so their spans use one coordinate model. A value type with
/// structural equality so it participates in the owning node's value comparison — the incremental-caching
/// contract of the derived parsers ([V01.01.05.01]/[V01.01.06.01]).
/// </summary>
/// <remarks>
/// Offsets are relative to whatever source string the owning parser was handed (block content for the
/// template compiler, the whole <c>.viu</c> file for the single-file-component parser); mapping between
/// those coordinate spaces is the consumer's job ([V01.01.06.03]).
/// </remarks>
/// <param name="Offset">Zero-based character offset from the start of the source.</param>
/// <param name="Line">One-based line number.</param>
/// <param name="Column">One-based column number.</param>
public readonly record struct Position(int Offset, int Line, int Column);
