using System;
using System.ComponentModel.Composition;
using System.Globalization;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Traces what a <c>.viu</c> editor view actually looks like when it opens, and every buffer change
/// made while it is open ([V01.01.12.07.09]).
/// </summary>
/// <remarks>
/// <para>
/// <b>This listener does not change anything.</b> It is named for what it does: observe. The repair
/// itself lives in <see cref="ViuBraceCompletionEnablementTextViewCreationListener"/>, which is
/// product code and runs whether or not the trace is on. This part is what found that repair: its
/// first line established that a Viu view opens with the brace-completion option explicitly defined
/// <see langword="false"/> on the view while the global value is <see langword="true"/> — which no
/// amount of reading the decompiled editor had suggested, because the writer sets the option by name
/// string rather than by key.
/// </para>
/// <para>
/// Its option reading is labelled <c>braceCompletionAsObserved</c> on purpose: two
/// <see cref="ITextViewCreationListener"/> parts have no defined order between them, so this may run
/// before or after the repair. The <c>brace.reenabled</c> line carries the authoritative before and
/// after.
/// </para>
/// <para>
/// <b>The buffer-change trace is the decisive one.</b> A pair that never appears and a pair that
/// appears and is then removed look identical to a user and completely different in this log: the
/// first shows one insertion of the typed character, the second shows two insertions followed by a
/// deletion. Whoever is responsible is whoever ran between those lines.
/// </para>
/// <para>
/// Dormant unless <c>VIU_EDITOR_DIAGNOSTICS</c> is set: with the trace off the whole body is one
/// static field read, no event is subscribed, and no file is touched.
/// </para>
/// </remarks>
[Export(typeof(ITextViewCreationListener))]
[ContentType(ViuContentTypes.Viu)]
[TextViewRole(PredefinedTextViewRoles.Document)]
[TextViewRole(PredefinedTextViewRoles.Editable)]
internal sealed class ViuDiagnosticsTextViewCreationListener : ITextViewCreationListener
{
    /// <inheritdoc />
    public void TextViewCreated(ITextView textView)
    {
        if (!ViuEditorDiagnostics.IsEnabled || textView is null)
        {
            return;
        }

        ViuEditorDiagnostics.Trace("view.created", () => string.Concat(
            "viewBuffer=", ViuEditorDiagnosticsDescriptions.DescribeContentType(textView.TextBuffer?.ContentType),
            " | dataModel=", ViuEditorDiagnosticsDescriptions.DescribeContentType(textView.TextDataModel?.ContentType),
            " | documentBufferIsViewBuffer=",
            ReferenceEquals(textView.TextBuffer, textView.TextDataModel?.DocumentBuffer)
                .ToString(CultureInfo.InvariantCulture),
            " | roles=", ViuEditorDiagnosticsDescriptions.DescribeRoles(textView.Roles),
            // "AsObserved" because two ITextViewCreationListener parts have no defined order between
            // them: this may run before or after ViuBraceCompletionEnablementTextViewCreationListener
            // repairs the option. The brace.reenabled line carries the authoritative before/after.
            " | braceCompletionAsObserved(",
            ViuEditorDiagnosticsDescriptions.DescribeBraceCompletionOption(textView.Options), ")",
            " | ", ViuEditorDiagnosticsDescriptions.DescribeBraceCompletionManager(textView)));

        SubscribeToBufferChanges(textView);
    }

    private static void SubscribeToBufferChanges(ITextView textView)
    {
        ITextBuffer? buffer = textView.TextBuffer;
        if (buffer is null)
        {
            return;
        }

        void OnChanged(object sender, TextContentChangedEventArgs arguments) =>
            ViuEditorDiagnostics.Trace("buffer.changed", () => string.Concat(
                "version=", arguments.Before.Version.VersionNumber.ToString(CultureInfo.InvariantCulture),
                "->", arguments.After.Version.VersionNumber.ToString(CultureInfo.InvariantCulture),
                " reason=", arguments.EditTag?.ToString() ?? "<none>",
                " changes=", arguments.Changes.Count.ToString(CultureInfo.InvariantCulture),
                " ", ViuEditorDiagnosticsDescriptions.DescribeChanges(arguments.Changes)));

        void OnClosed(object sender, EventArgs arguments)
        {
            buffer.Changed -= OnChanged;
            textView.Closed -= OnClosed;
            ViuEditorDiagnostics.Trace("view.closed", () => "buffer trace detached");
        }

        buffer.Changed += OnChanged;
        textView.Closed += OnClosed;
    }
}
