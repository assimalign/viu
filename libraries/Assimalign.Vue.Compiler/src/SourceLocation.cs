namespace Assimalign.Vue.Compiler;

/// <summary>
/// The half-open source range <c>[Start, End)</c> a node spans, plus the exact original source slice.
/// The C# port of Vue 3.5's <c>SourceLocation</c> interface (<c>@vue/compiler-core</c> <c>ast.ts</c>).
/// </summary>
/// <remarks>
/// The incremental-caching contract of [V01.01.05.01] requires that, for every node the parser emits,
/// <see cref="Source"/> equals <c>template.Substring(Start.Offset, End.Offset - Start.Offset)</c> — the
/// literal characters between the two positions. A <c>record</c> so equal ranges compare equal.
/// </remarks>
/// <param name="Start">The inclusive start position.</param>
/// <param name="End">The exclusive end position.</param>
/// <param name="Source">The exact source substring covered by the range.</param>
public sealed record SourceLocation(Position Start, Position End, string Source);
