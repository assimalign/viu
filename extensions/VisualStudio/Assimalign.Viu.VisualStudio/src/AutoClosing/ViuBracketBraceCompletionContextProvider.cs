using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.BraceCompletion;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Registers the bracket pairs a <c>.viu</c> document completes, and gives their sessions the Viu
/// half that expands a <c>{ }</c> block on <c>Enter</c> ([V01.01.12.07.09]).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a context provider rather than the default provider these pairs shipped on.</b> The editor
/// runs a brace-completion session out of the box — it inserts the closer, tracks the span, types
/// through the closer, and deletes both halves on one backspace — and none of that needs a
/// language-specific context. Exactly one behavior does: <c>OnReturn</c>. The editor's session calls
/// it only when a context is present (<c>BraceCompletionDefaultSession.PostReturn</c> guards on
/// <c>_context != null</c>), so a pair registered on an <see cref="IBraceCompletionDefaultProvider"/>
/// can never expand into a block, which is what [V01.01.12.07.08] recorded as deferred. Carrying the
/// three pairs here instead is what closes that gap; nothing else about their behavior changes,
/// because the aggregator builds the same <c>BraceCompletionDefaultSession</c> for a context provider
/// that it builds for a default one.
/// </para>
/// <para>
/// <b>The pairs stay ungated</b> — <see cref="ViuAutoClosingLogic.AllowsBracketPair"/> is a pure
/// statement of the shipped matrix (all three brackets pair in every section) rather than a
/// computation, and it is unit-tested as such. It is still asked, because a context provider is asked
/// and because that is where any future position rule belongs.
/// </para>
/// <para>
/// <b>The quotes stay where they are.</b> They pair only in some positions, and
/// <see cref="ViuQuoteBraceCompletionContextProvider"/> is their gate; splitting the two providers
/// keeps a gated pair and an ungated one from sharing one <c>TryCreateContext</c>. Both hand out the
/// same stateless <see cref="ViuBraceCompletionContext"/>, which decides what to do from the
/// session's own opening brace.
/// </para>
/// <para>
/// Runtime-only surface: the MEF composition and the snapshot conversion below are verified by
/// running the extension, not by a unit test.
/// </para>
/// </remarks>
[Export(typeof(IBraceCompletionContextProvider))]
[BracePair('{', '}')]
[BracePair('(', ')')]
[BracePair('[', ']')]
[ContentType(ViuContentTypes.Viu)]
internal sealed class ViuBracketBraceCompletionContextProvider : IBraceCompletionContextProvider
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

        bool allowed = ViuAutoClosingLogic.AllowsBracketPair(
            ViuSnapshotLines.Read(snapshot),
            line.LineNumber,
            openingPoint.Position - line.Start.Position,
            openingBrace);

        // Reaching this method at all is the fact worth recording ([V01.01.12.07.09]): it proves the
        // editor's aggregator resolved a Viu provider for the typed character, which no amount of
        // reading the decompiled editor can prove for a particular machine.
        if (ViuEditorDiagnostics.IsEnabled)
        {
            ViuEditorDiagnostics.Trace("context.bracket", () => string.Concat(
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
