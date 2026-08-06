namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// A value-equatable snapshot of a source range in a <c>.viu</c> file — the file path plus zero-based
/// character offsets and line/character positions — so parser diagnostics can ride inside the
/// incremental generator's cached model without dragging a non-equatable Roslyn <c>Location</c> (and
/// its <c>SyntaxTree</c>) into the cache. Positions are stored zero-based (Roslyn's convention); the
/// base cluster's <c>Position</c> is one-based for line/column, so the conversion happens once when
/// this snapshot is built. Host-neutral ([V01.01.06.11]): the generator's diagnostic adapter rebuilds
/// the Roslyn <c>Location</c>, and the language service reads the zero-based positions directly.
/// </summary>
internal readonly record struct LocationInfo(
    string FilePath,
    int StartOffset,
    int EndOffset,
    int StartLine,
    int StartCharacter,
    int EndLine,
    int EndCharacter);
