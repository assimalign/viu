using System;
using System.Collections.Generic;
using System.Threading;

using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Applies an attribute completion itself so the caret lands in the value, and declines everything
/// else to the editor's own commit.
/// </summary>
/// <remarks>
/// The decision is <see cref="ViuCompletionCommitLogic"/>'s; this type only translates it into an
/// editor edit. Declining is the default in every uncertain case — a caret that cannot be read, a
/// buffer that changed underneath, an item the rule does not claim — because a commit adapter that
/// guesses wrong breaks typing, and the editor's own commit is always a correct answer.
/// </remarks>
internal sealed class ViuCompletionCommitManager : IAsyncCompletionCommitManager
{
    /// <inheritdoc />
    /// <remarks>
    /// Empty: the adapter changes how an item commits, never which characters commit it, so the
    /// editor's own commit characters continue to decide that.
    /// </remarks>
    public IEnumerable<char> PotentialCommitCharacters => Array.Empty<char>();

    /// <inheritdoc />
    public bool ShouldCommitCompletion(
        IAsyncCompletionSession session,
        SnapshotPoint location,
        char typedCharacter,
        CancellationToken token) => true;

    /// <inheritdoc />
    public CommitResult TryCommit(
        IAsyncCompletionSession session,
        ITextBuffer buffer,
        CompletionItem item,
        char typedCharacter,
        CancellationToken token)
    {
        if (session is null || buffer is null || item is null)
        {
            return CommitResult.Unhandled;
        }

        ITextView textView = session.TextView;
        SnapshotPoint? caretPoint = textView?.Caret.Position.Point.GetPoint(
            buffer,
            PositionAffinity.Predecessor);
        if (caretPoint is null)
        {
            return CommitResult.Unhandled;
        }

        ITextSnapshotLine line = caretPoint.Value.Snapshot.GetLineFromPosition(
            caretPoint.Value.Position);
        ViuCompletionCommitEdit? commitEdit = ViuCompletionCommitLogic.GetAttributeCommitEdit(
            line.GetText(),
            caretPoint.Value.Position - line.Start.Position,
            item.InsertText);
        if (commitEdit is null)
        {
            return CommitResult.Unhandled;
        }

        int replaceStart = line.Start.Position + commitEdit.ReplaceStart;
        using (ITextEdit edit = buffer.CreateEdit())
        {
            edit.Replace(replaceStart, commitEdit.ReplaceLength, commitEdit.Text);
            edit.Apply();
        }

        MoveCaret(textView!, buffer, replaceStart + commitEdit.CaretOffset);
        return CommitResult.Handled;
    }

    private static void MoveCaret(ITextView textView, ITextBuffer buffer, int position)
    {
        ITextSnapshot snapshot = buffer.CurrentSnapshot;
        if (position < 0 || position > snapshot.Length)
        {
            return;
        }

        SnapshotPoint subjectPoint = new(snapshot, position);
        SnapshotPoint? viewPoint = ReferenceEquals(buffer, textView.TextBuffer)
            ? subjectPoint
            : textView.BufferGraph.MapUpToBuffer(
                subjectPoint,
                PointTrackingMode.Positive,
                PositionAffinity.Successor,
                textView.TextBuffer);
        if (viewPoint is { } caretPoint)
        {
            textView.Caret.MoveTo(caretPoint);
        }
    }
}
