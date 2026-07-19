using System.Collections.Generic;

namespace Assimalign.Vue.Syntax.Css;

/// <summary>
/// Maps character offsets in a CSS source string to <see cref="Position"/> values (offset plus
/// one-based line and column), and materializes <see cref="SourceLocation"/> ranges whose
/// <see cref="SourceLocation.Source"/> is the exact original slice — the exact-slice invariant every
/// <c>Assimalign.Vue.Syntax.*</c> node upholds. Newline offsets are recorded once up front so each
/// lookup is a scan bounded by the line count.
/// </summary>
internal sealed class CssPositionMap
{
    private readonly string source;
    private readonly List<int> newlineOffsets = new List<int>();

    /// <summary>Builds the map over <paramref name="source"/>, recording every <c>\n</c> offset.</summary>
    /// <param name="source">The CSS source string.</param>
    public CssPositionMap(string source)
    {
        this.source = source;
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index] == '\n')
            {
                newlineOffsets.Add(index);
            }
        }
    }

    /// <summary>The length of the mapped source.</summary>
    public int Length => source.Length;

    /// <summary>Builds the <see cref="Position"/> for <paramref name="offset"/>.</summary>
    /// <param name="offset">The zero-based character offset.</param>
    /// <returns>The position — offset plus one-based line and column.</returns>
    public Position PositionAt(int offset)
    {
        var line = 1;
        var column = offset + 1;
        for (var index = newlineOffsets.Count - 1; index >= 0; index--)
        {
            var newlineOffset = newlineOffsets[index];
            if (offset > newlineOffset)
            {
                line = index + 2;
                column = offset - newlineOffset;
                break;
            }
        }

        return new Position(offset, line, column);
    }

    /// <summary>
    /// Builds the <see cref="SourceLocation"/> for the half-open range <c>[start, end)</c>, slicing the
    /// exact source substring so the exact-slice invariant holds.
    /// </summary>
    /// <param name="start">The inclusive start offset.</param>
    /// <param name="end">The exclusive end offset.</param>
    /// <returns>The located range with its exact source slice.</returns>
    public SourceLocation LocationOf(int start, int end)
        => new SourceLocation(PositionAt(start), PositionAt(end), source.Substring(start, end - start));
}
