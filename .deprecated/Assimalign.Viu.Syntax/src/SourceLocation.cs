namespace Assimalign.Viu.Syntax;

/// <summary>
/// The half-open source range <c>[Start, End)</c> a node spans, plus the exact original source slice.
/// The C# port of Vue 3.5's <c>SourceLocation</c> interface (<c>@vue/compiler-core</c> <c>ast.ts</c>,
/// re-exported by <c>@vue/compiler-sfc</c>). The shared span type of every <c>Assimalign.Viu.Syntax.*</c>
/// parser.
/// </summary>
/// <remarks>
/// The incremental-caching contract of the derived parsers ([V01.01.05.01]/[V01.01.06.01]) requires that,
/// for every node a parser emits, <see cref="Source"/> equals
/// <c>source.Substring(Start.Offset, End.Offset - Start.Offset)</c> — the literal characters between the
/// two positions. A <c>record</c> so equal ranges compare equal. Offsets are in the owning parser's own
/// coordinate space (see <see cref="Position"/>).
/// </remarks>
/// <param name="Start">The inclusive start position.</param>
/// <param name="End">The exclusive end position.</param>
/// <param name="Source">The exact source substring covered by the range.</param>
public sealed record SourceLocation(Position Start, Position End, string Source);
