using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.BraceCompletion;
using Microsoft.VisualStudio.Text.Editor;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// The language-specific half of a Viu brace-completion session: everything the editor's own session
/// does, plus the C# block expansion a <c>{ }</c> pair takes on <c>Enter</c> inside a script section
/// ([V01.01.12.07.08], [V01.01.12.07.09]).
/// </summary>
/// <remarks>
/// <para>
/// One context serves every pair the extension registers, because the session it is handed carries
/// the only thing that distinguishes them — <see cref="IBraceCompletionSession.OpeningBrace"/>. The
/// quotes want nothing beyond the editor's behavior; the brackets want nothing either, except that
/// <c>{</c> expands on <c>Enter</c>. Holding no state, one shared instance is enough.
/// </para>
/// <para>
/// <b>What the editor already does, and this deliberately does not repeat.</b> Inserting the pair,
/// tracking its span, typing through the closing character, and deleting both halves on a single
/// backspace are all <c>BraceCompletionDefaultSession</c> behaviors that run with or without a
/// context — over-typing consults <see cref="AllowOverType"/> only to let a context <em>veto</em> it
/// (<c>OvertypeSession.PreOverType</c> tests <c>_braceCompletionContext != null &amp;&amp;
/// !AllowOverType(...)</c>), and paired backspace never asks a context at all. The one behavior the
/// editor delegates outright is <see cref="OnReturn"/>, and that is the whole reason this type exists.
/// </para>
/// <para>
/// Runtime-only surface: the buffer edit and caret move in <see cref="OnReturn"/> are verified by
/// running the extension. The two decisions behind them —
/// <see cref="ViuAutoClosingLogic.AllowsBlockExpansionOnReturn"/> and
/// <see cref="ViuBraceIndentation.ComputeBlockExpansion"/> — are pure and unit-tested.
/// </para>
/// </remarks>
internal sealed class ViuBraceCompletionContext : IBraceCompletionContext
{
    /// <summary>The single shared, stateless context.</summary>
    public static readonly ViuBraceCompletionContext Instance = new();

    private ViuBraceCompletionContext()
    {
    }

    /// <inheritdoc />
    public void Start(IBraceCompletionSession session)
    {
    }

    /// <inheritdoc />
    public void Finish(IBraceCompletionSession session)
    {
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Expands <c>{|}</c> into a three-line block: the closing brace moves onto its own line at the
    /// opening brace line's indentation, and the caret lands on a blank line one level further in.
    /// That is the shape Visual Studio's C# editor produces for an empty block, and matching it is
    /// the point of the work item.
    /// </para>
    /// <para>
    /// The editor has already inserted the line break and its own indentation by the time this runs —
    /// <c>BraceCompletionDefaultSession.PostReturn</c> is a <em>post</em> hook — so the work is a
    /// single replacement of the whitespace between the caret's line start and the closing brace.
    /// One <see cref="ITextEdit"/> is one undo unit; it cannot be merged with the line break the
    /// editor already committed, so <c>Ctrl+Z</c> steps back through the expansion and then the
    /// newline. Every guard below is a bail-out rather than a repair: a session whose points have
    /// gone stale should leave the buffer exactly as the editor left it.
    /// </para>
    /// </remarks>
    public void OnReturn(IBraceCompletionSession session)
    {
        // Reaching OnReturn proves the session carries a Viu context, which is the whole point of the
        // provider reshaping ([V01.01.12.07.09]).
        if (ViuEditorDiagnostics.IsEnabled)
        {
            ViuEditorDiagnostics.Trace("context.onreturn", () => string.Concat(
                "open=", ViuEditorDiagnostics.Describe(session.OpeningBrace),
                " close=", ViuEditorDiagnostics.Describe(session.ClosingBrace),
                " hasPoints=",
                (session.OpeningPoint is not null && session.ClosingPoint is not null).ToString()));
        }

        // Only a brace opens a block. A parenthesis or a square bracket keeps the editor's plain
        // behavior on Return, exactly as C# does.
        if (session.OpeningBrace != '{' ||
            session.OpeningPoint is not { } openingTrackingPoint ||
            session.ClosingPoint is not { } closingTrackingPoint)
        {
            return;
        }

        ITextView textView = session.TextView;
        ITextBuffer subjectBuffer = session.SubjectBuffer;
        ITextSnapshot snapshot = subjectBuffer.CurrentSnapshot;

        // ClosingPoint tracks the position after the closing brace, which is what makes the pair's
        // span deletable in one go; the brace itself is the character before it.
        int closingBracePosition = closingTrackingPoint.GetPoint(snapshot).Position - 1;
        int openingBracePosition = openingTrackingPoint.GetPoint(snapshot).Position;
        if (closingBracePosition <= openingBracePosition)
        {
            return;
        }

        SnapshotPoint? caretPoint = textView.Caret.Position.Point.GetPoint(
            subjectBuffer,
            PositionAffinity.Predecessor);
        if (caretPoint is not { } caret || caret.Position > closingBracePosition)
        {
            return;
        }

        ITextSnapshotLine openingBraceLine = snapshot.GetLineFromPosition(openingBracePosition);
        ITextSnapshotLine caretLine = snapshot.GetLineFromPosition(caret.Position);
        if (caretLine.LineNumber <= openingBraceLine.LineNumber ||
            !IsWhitespaceRange(snapshot, caretLine.Start.Position, closingBracePosition))
        {
            return;
        }

        if (!ViuAutoClosingLogic.AllowsBlockExpansionOnReturn(
                ViuSnapshotLines.Read(snapshot),
                openingBraceLine.LineNumber))
        {
            return;
        }

        IEditorOptions options = textView.Options;
        ViuBlockExpansion expansion = ViuBraceIndentation.ComputeBlockExpansion(
            openingBraceLine.GetText(),
            openingBracePosition - openingBraceLine.Start.Position,
            options.GetOptionValue(DefaultOptions.IndentSizeOptionId),
            options.GetOptionValue(DefaultOptions.ConvertTabsToSpacesOptionId));

        // The opening line's own break, so the expansion cannot introduce a line ending the file does
        // not already use; the option is the fallback for a document with a single line.
        string lineBreak = openingBraceLine.GetLineBreakText();
        if (string.IsNullOrEmpty(lineBreak))
        {
            lineBreak = options.GetOptionValue(DefaultOptions.NewLineCharacterOptionId);
        }

        int replacementStart = caretLine.Start.Position;
        // The brace's move happens in the same edit, and therefore the same undo unit: one Return
        // produced one block, and stepping back through half of it would leave a shape nobody typed.
        // Both spans are measured against this snapshot and cannot overlap - the brace's line is
        // above the caret's.
        int openingBraceMoveStart = expansion.OpeningBraceReplaceStart < 0
            ? -1
            : openingBraceLine.Start.Position + expansion.OpeningBraceReplaceStart;
        int caretDelta = 0;
        using (ITextEdit edit = subjectBuffer.CreateEdit())
        {
            if (openingBraceMoveStart >= 0)
            {
                string movedBrace = lineBreak + expansion.ClosingBraceIndentation + "{";
                caretDelta = movedBrace.Length - (openingBracePosition + 1 - openingBraceMoveStart);
                edit.Replace(
                    Span.FromBounds(openingBraceMoveStart, openingBracePosition + 1),
                    movedBrace);
            }

            edit.Replace(
                Span.FromBounds(replacementStart, closingBracePosition),
                expansion.CaretIndentation + lineBreak + expansion.ClosingBraceIndentation);
            if (edit.HasFailedChanges)
            {
                edit.Cancel();
                return;
            }

            edit.Apply();
        }

        MoveCaret(
            textView,
            subjectBuffer,
            replacementStart + caretDelta + expansion.CaretIndentation.Length);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Typing the closing character inside the session always moves through it rather than inserting
    /// a second one: the editor only asks when there is nothing but whitespace between the caret and
    /// the closer, so the only way to reach this question is for the user to be finishing the pair
    /// the session opened.
    /// </remarks>
    public bool AllowOverType(IBraceCompletionSession session) => true;

    private static bool IsWhitespaceRange(ITextSnapshot snapshot, int start, int end)
    {
        for (int position = start; position < end; position++)
        {
            if (!char.IsWhiteSpace(snapshot[position]))
            {
                return false;
            }
        }

        return true;
    }

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
