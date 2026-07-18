namespace Assimalign.Vue.SingleFileComponent;

/// <summary>
/// The half-open source range <c>[Start, End)</c> a block or option spans, plus the exact original
/// source slice. Mirrors Vue 3.5's <c>SourceLocation</c> interface (<c>@vue/compiler-core</c>
/// <c>ast.ts</c>); the <c>.viu</c> block parser produces one for every block (both the block as a whole
/// and its content region) and for every option.
/// </summary>
/// <remarks>
/// For every range the parser emits, <see cref="Source"/> equals
/// <c>source.Substring(Start.Offset, End.Offset - Start.Offset)</c> — the literal characters between the
/// two positions. A <c>record</c> so equal ranges compare equal, which (together with the immutable
/// block models) is what lets an incremental generator ([V01.01.06.02]) cache on the parse output, and
/// what makes the spans suitable for <c>#line</c> mapping ([V01.01.06.03]) and IDE diagnostics.
/// </remarks>
/// <param name="Start">The inclusive start position.</param>
/// <param name="End">The exclusive end position.</param>
/// <param name="Source">The exact source substring covered by the range.</param>
public sealed record SingleFileComponentSourceLocation(SingleFileComponentPosition Start, SingleFileComponentPosition End, string Source);
