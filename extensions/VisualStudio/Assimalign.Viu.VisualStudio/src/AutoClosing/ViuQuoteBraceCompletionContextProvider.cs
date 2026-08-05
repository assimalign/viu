using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.BraceCompletion;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Starts a quote-completion session only where a quote means a string ([V01.01.12.07.08]).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a context provider rather than a default one.</b> The editor's brace-completion aggregator
/// asks the providers registered for a character in order — session, dynamic session, context, then
/// default — and takes the first one that agrees to start. A default provider always agrees, so a
/// pair registered on one could not be declined; registering the quotes on an
/// <see cref="IBraceCompletionContextProvider"/> instead makes
/// <see cref="TryCreateContext"/> the gate. That is also why the quotes are registered <em>here</em>
/// and nowhere else: registering them a second time on a default provider would give the aggregator
/// an unconditional fallback and defeat the gate entirely.
/// </para>
/// <para>
/// <b>The gate itself</b> is <see cref="ViuAutoClosingLogic.AllowsQuotePair"/> — pure, section-aware,
/// and unit-tested. This part only converts the editor's snapshot and point into the line-and-offset
/// form that decision takes, so the interesting behavior is covered without the editor in the way.
/// The gating is unchanged by [V01.01.12.07.09]; only the brackets moved onto a context provider of
/// their own (<see cref="ViuBracketBraceCompletionContextProvider"/>).
/// </para>
/// <para>
/// <b>Auto-surround is metadata-driven and therefore ungated.</b> Selecting text and typing a quote
/// wraps the selection wherever the pair is registered, because the editor answers that from the
/// <see cref="BracePairAttribute"/> alone and never consults this provider. That is accepted rather
/// than worked around: surrounding only happens with an active selection, where the alternative — the
/// editor replacing the selection with the typed character — is the more destructive outcome.
/// </para>
/// <para>
/// Runtime-only surface: the MEF composition and the snapshot conversion below are verified by
/// running the extension, not by a unit test.
/// </para>
/// </remarks>
[Export(typeof(IBraceCompletionContextProvider))]
[BracePair('"', '"')]
[BracePair('\'', '\'')]
[ContentType(ViuContentTypes.Viu)]
internal sealed class ViuQuoteBraceCompletionContextProvider : IBraceCompletionContextProvider
{
    /// <inheritdoc />
    public bool TryCreateContext(
        ITextView textView,
        SnapshotPoint openingPoint,
        char openingBrace,
        char closingBrace,
        out IBraceCompletionContext context)
    {
        ITextSnapshot snapshot = openingPoint.Snapshot;
        ITextSnapshotLine line = snapshot.GetLineFromPosition(openingPoint.Position);

        bool allowed = ViuAutoClosingLogic.AllowsQuotePair(
            ViuSnapshotLines.Read(snapshot),
            line.LineNumber,
            openingPoint.Position - line.Start.Position,
            openingBrace);

        // Traced for the same reason as the bracket provider ([V01.01.12.07.09]): whether the editor
        // consults a Viu provider at all is the question, and the quotes answer it for a second
        // character class.
        if (ViuEditorDiagnostics.IsEnabled)
        {
            ViuEditorDiagnostics.Trace("context.quote", () => string.Concat(
                "open=", ViuEditorDiagnostics.Describe(openingBrace),
                " close=", ViuEditorDiagnostics.Describe(closingBrace),
                " ", ViuEditorDiagnosticsDescriptions.DescribePosition(snapshot, openingPoint.Position),
                " allowed=", allowed.ToString(),
                " ", ViuEditorDiagnosticsDescriptions.DescribeBraceCompletionManager(textView)));
        }

        if (allowed)
        {
            context = ViuBraceCompletionContext.Instance;
            return true;
        }

        context = null!;
        return false;
    }
}
