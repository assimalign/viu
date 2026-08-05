using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Closes elements and comments as they are typed in a <c>.viu</c> template ([V01.01.12.07.08]).
/// </summary>
/// <remarks>
/// <para>
/// A thin adapter over <see cref="ViuAutoClosingLogic.GetTypedCharacterCompletion"/>: it converts
/// the caret into a line and offset, asks what — if anything — should be inserted, and applies that
/// answer to the buffer. Every rule about <em>what</em> to insert, and every rule about where an
/// insertion is forbidden, lives in that pure decision class and is unit-tested there.
/// </para>
/// <para>
/// <b>Element auto-close has no user option, deliberately.</b> Character pairs ride the editor's
/// Automatic Brace Completion option; this behavior has no editor-owned equivalent, and inventing a
/// Viu options page for one toggle was rejected in favor of shipping it always on. Recorded in
/// <c>docs/DESIGN.md</c>; revisit if it proves intrusive in practice.
/// </para>
/// <para>
/// <b>Ordering.</b> The handler runs after the editor's completion and brace-completion handlers.
/// Both of those are chained handlers that pass the character along, so they see it first and this
/// one still runs; the order matters because this handler reports the command as handled when it
/// acts, and a handled command stops the chain — running first would hide the keystroke from an open
/// completion session. The two names are written as literals rather than imported: one of them
/// (<c>BraceCompletionCommandHandler</c>) has no published constant at all, so a package reference
/// for the other would buy nothing, and an ordering name the composition does not recognize is
/// simply ignored.
/// </para>
/// <para>
/// <b>Undo.</b> The typed character is part of the inserted text rather than being left to the
/// editor, so the completion is a single buffer change inside a single transaction: one
/// <c>Ctrl+Z</c> removes the end tag and the character that triggered it together, which is how the
/// editor's own brace completion and Visual Studio's HTML editor behave.
/// </para>
/// <para>
/// Runtime-only surface: the MEF export, the buffer edit, and the caret move below are verified by
/// running the extension, not by a unit test.
/// </para>
/// </remarks>
[Export(typeof(ICommandHandler))]
[Name(nameof(ViuAutoClosingCommandHandler))]
[ContentType(ViuContentTypes.Viu)]
[TextViewRole(PredefinedTextViewRoles.Editable)]
[Order(After = "CompletionCommandHandler")]
[Order(After = "BraceCompletionCommandHandler")]
internal sealed class ViuAutoClosingCommandHandler : ICommandHandler<TypeCharCommandArgs>
{
    private readonly ITextUndoHistoryRegistry undoHistoryRegistry;

    /// <summary>
    /// Initializes the handler with the registry it takes the buffer's undo history from.
    /// </summary>
    /// <param name="undoHistoryRegistry">The editor's undo history registry.</param>
    [ImportingConstructor]
    public ViuAutoClosingCommandHandler(ITextUndoHistoryRegistry undoHistoryRegistry) =>
        this.undoHistoryRegistry = undoHistoryRegistry;

    /// <inheritdoc />
    public string DisplayName => "Viu automatic element closing";

    /// <inheritdoc />
    /// <remarks>
    /// Unspecified rather than available: this handler contributes no command of its own to any menu
    /// and only ever reacts to ordinary typing, so it must not influence the state the rest of the
    /// chain reports.
    /// </remarks>
    public CommandState GetCommandState(TypeCharCommandArgs args) => CommandState.Unspecified;

    /// <inheritdoc />
    public bool ExecuteCommand(TypeCharCommandArgs args, CommandExecutionContext executionContext)
    {
        if (!ViuAutoClosingLogic.IsCompletionTriggerCharacter(args.TypedChar))
        {
            return false;
        }

        ITextView textView = args.TextView;
        ITextBuffer subjectBuffer = args.SubjectBuffer;

        // Typing over a selection replaces it; that is the editor's job, and an auto-closed end tag
        // around a replacement would be guesswork.
        if (!textView.Selection.IsEmpty)
        {
            return false;
        }

        SnapshotPoint? caretPoint = textView.Caret.Position.Point.GetPoint(
            subjectBuffer,
            PositionAffinity.Successor);
        if (caretPoint is null)
        {
            return false;
        }

        ITextSnapshot snapshot = caretPoint.Value.Snapshot;
        ITextSnapshotLine line = snapshot.GetLineFromPosition(caretPoint.Value.Position);

        ViuAutoClosingEdit? completion = ViuAutoClosingLogic.GetTypedCharacterCompletion(
            ViuSnapshotLines.Read(snapshot),
            line.LineNumber,
            caretPoint.Value.Position - line.Start.Position,
            args.TypedChar);
        if (completion is not { } autoClosingEdit)
        {
            return false;
        }

        this.ApplyCompletion(textView, subjectBuffer, caretPoint.Value.Position, autoClosingEdit);
        return true;
    }

    private void ApplyCompletion(
        ITextView textView,
        ITextBuffer subjectBuffer,
        int insertPosition,
        ViuAutoClosingEdit autoClosingEdit)
    {
        this.undoHistoryRegistry.TryGetHistory(subjectBuffer, out ITextUndoHistory undoHistory);
        ITextUndoTransaction? transaction = undoHistory?.CreateTransaction(this.DisplayName);

        try
        {
            using (ITextEdit edit = subjectBuffer.CreateEdit())
            {
                edit.Insert(insertPosition, autoClosingEdit.InsertedText);
                edit.Apply();
            }

            MoveCaret(
                textView,
                subjectBuffer,
                insertPosition + autoClosingEdit.CaretOffset);
            transaction?.Complete();
        }
        finally
        {
            transaction?.Dispose();
        }
    }

    // The caret is placed explicitly because an insertion at the caret otherwise carries it to the
    // end of the inserted text, which is the wrong end for every completion but the closing tag.
    private static void MoveCaret(ITextView textView, ITextBuffer subjectBuffer, int position)
    {
        ITextSnapshot snapshot = subjectBuffer.CurrentSnapshot;
        if (position > snapshot.Length)
        {
            return;
        }

        SnapshotPoint subjectPoint = new(snapshot, position);
        SnapshotPoint? viewPoint = ReferenceEquals(subjectBuffer, textView.TextBuffer)
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
