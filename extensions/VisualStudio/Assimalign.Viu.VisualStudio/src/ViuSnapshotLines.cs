using System.Collections.Generic;

using Microsoft.VisualStudio.Text;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Reads a text snapshot into the line list the editor-free Viu analyses take as input.
/// </summary>
/// <remarks>
/// The lexer, the section scanner, and the auto-closing decisions all work on lines without their
/// line breaks and know nothing of <see cref="ITextSnapshot"/>; this is the single place the editor's
/// buffer model is converted into that shape.
/// </remarks>
internal static class ViuSnapshotLines
{
    /// <summary>
    /// Copies a snapshot's text into one string per line, in document order.
    /// </summary>
    /// <param name="snapshot">The snapshot to read.</param>
    /// <returns>The snapshot's lines, without their line breaks.</returns>
    public static IReadOnlyList<string> Read(ITextSnapshot snapshot)
    {
        int lineCount = snapshot.LineCount;
        List<string> lines = new(lineCount);
        for (int lineNumber = 0; lineNumber < lineCount; lineNumber++)
        {
            lines.Add(snapshot.GetLineFromLineNumber(lineNumber).GetText());
        }

        return lines;
    }
}
