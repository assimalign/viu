using System.Collections.Generic;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Closes elements and comments, and composes the interpolation scaffold, as they are typed in a
/// <c>.viu</c> template ([V01.01.12.07.08], [V01.01.12.07.09]).
/// </summary>
/// <remarks>
/// <para>
/// A thin adapter over <see cref="ViuAutoClosingLogic.GetTypedCharacterCompletion"/>: it converts
/// the caret into a line and offset, asks what — if anything — should be inserted, and applies that
/// answer to the buffer. Every rule about <em>what</em> to insert, and every rule about where an
/// insertion is forbidden, lives in that pure decision class and is unit-tested there.
/// </para>
/// <para>
/// <b>The interpolation scaffold cannot race a brace-completion session</b>, and that is arranged
/// rather than hoped for. <see cref="ViuAutoClosingLogic.AllowsBracketPair"/> declines the second
/// <c>{</c> of a template interpolation, so <see cref="ViuBracketBraceCompletionContextProvider"/>
/// refuses the context, the aggregator creates nothing, and the editor's manager holds no pending
/// session to complete after this handler writes. Without that decline the manager's
/// <c>PostTypeChar</c> would validate the caret it finds after the scaffold — the character before it
/// really is <c>{</c> — push a session, and insert a third brace.
/// </para>
/// <para>
/// The session opened by the <em>first</em> <c>{</c>, where there was one, is left alone. Its
/// tracking points survive the scaffold's insertion and simply widen around it; its over-type path
/// then fails its own validity check because the span's content has changed, so it declines the
/// closing brace and the walk-over below answers instead. A session that dies quietly here costs
/// nothing: everything it would have done is done explicitly.
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
        if (ViuEditorDiagnostics.IsEnabled)
        {
            ViuEditorDiagnostics.Trace("typechar.enter", () => string.Concat(
                "char=", ViuEditorDiagnostics.Describe(args.TypedChar),
                " isTriggerCharacter=",
                ViuAutoClosingLogic.IsCompletionTriggerCharacter(args.TypedChar).ToString(),
                " ", ViuEditorDiagnosticsDescriptions.DescribeBraceCompletionManager(args.TextView)));
        }

        bool handled = false;
        string outcome = "declined: not a trigger character";
        try
        {
            handled = this.TryComplete(args, out outcome);
            return handled;
        }
        finally
        {
            // A non-chained ICommandHandler has no next-handler delegate to bracket: the chain
            // continues precisely when this returns false, and stops - taking the editor's own typing
            // and BraceCompletionManager.PostTypeChar with it - when it returns true. Recording both
            // the value and the reason is therefore the same evidence a before/after pair would be
            // in a chained handler ([V01.01.12.07.09]).
            if (ViuEditorDiagnostics.IsEnabled)
            {
                bool handledOutcome = handled;
                string reason = outcome;
                ViuEditorDiagnostics.Trace("typechar.exit", () => string.Concat(
                    "char=", ViuEditorDiagnostics.Describe(args.TypedChar),
                    " handled=", handledOutcome.ToString(),
                    handledOutcome
                        ? " chain=STOPPED (this handler wrote the text)"
                        : " chain=CONTINUES (editor default typing and PostTypeChar still run)",
                    " outcome=", reason));
            }
        }
    }

    private bool TryComplete(TypeCharCommandArgs args, out string outcome)
    {
        if (!ViuAutoClosingLogic.IsCompletionTriggerCharacter(args.TypedChar))
        {
            outcome = "declined: not a trigger character";
            return false;
        }

        ITextView textView = args.TextView;
        ITextBuffer subjectBuffer = args.SubjectBuffer;

        // Typing over a selection replaces it; that is the editor's job, and an auto-closed end tag
        // around a replacement would be guesswork.
        if (!textView.Selection.IsEmpty)
        {
            outcome = "declined: selection is not empty";
            return false;
        }

        SnapshotPoint? caretPoint = textView.Caret.Position.Point.GetPoint(
            subjectBuffer,
            PositionAffinity.Successor);
        if (caretPoint is null)
        {
            outcome = "declined: caret does not map to the subject buffer";
            return false;
        }

        ITextSnapshot snapshot = caretPoint.Value.Snapshot;
        ITextSnapshotLine line = snapshot.GetLineFromPosition(caretPoint.Value.Position);
        IReadOnlyList<string> lines = ViuSnapshotLines.Read(snapshot);
        int characterIndex = caretPoint.Value.Position - line.Start.Position;

        // '}' is a caret move rather than an edit: the interpolation scaffold is written by hand, so
        // no brace-completion session tracks it and the editor's own type-through never applies.
        // Reaching here at all means the platform declined the character first - a live session's
        // PreOverType handles it before this chain link runs - so the two can never both act.
        if (args.TypedChar == '}')
        {
            if (!ViuAutoClosingLogic.AllowsClosingBraceWalkover(lines, line.LineNumber, characterIndex))
            {
                outcome = "declined: no closing brace to walk over";
                return false;
            }

            MoveCaret(textView, subjectBuffer, caretPoint.Value.Position + 1);
            outcome = "walked over an existing '}'";
            return true;
        }

        ViuAutoClosingEdit? completion = ViuAutoClosingLogic.GetTypedCharacterCompletion(
            lines,
            line.LineNumber,
            characterIndex,
            args.TypedChar);
        if (completion is not { } autoClosingEdit)
        {
            outcome = "declined: no completion at this position";
            return false;
        }

        this.ApplyCompletion(textView, subjectBuffer, caretPoint.Value.Position, autoClosingEdit);
        outcome = "completed with " + ViuEditorDiagnostics.Describe(autoClosingEdit.InsertedText);
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
