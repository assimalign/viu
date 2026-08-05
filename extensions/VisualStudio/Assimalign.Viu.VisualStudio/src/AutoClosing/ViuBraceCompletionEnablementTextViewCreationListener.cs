using System;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Removes the Automatic Brace Completion override Visual Studio's legacy editor adapter stamps onto
/// every <c>.viu</c> view, so the view inherits the user's own setting again ([V01.01.12.07.09]).
/// </summary>
/// <remarks>
/// <para>
/// <b>What goes wrong without this.</b> <c>SimpleTextViewWindow.Init_InitializeWpfTextView</c> calls
/// <c>SetToolsOptions</c> with the buffer's <em>language service</em> GUID, reads that language's
/// preferences through <c>IVsTextManager.GetUserPreferences7</c>, and copies them onto the new view's
/// options — including <c>_editorOptions.SetOptionValue("BraceCompletion/Enabled",
/// val.fBraceCompletion != 0)</c>. That field is backed by the legacy <c>ShowBraceCompletion</c>
/// registry value under <c>Languages\Language Services\&lt;Lang&gt;</c>, which C#, Razor, TypeScript,
/// HTML and JSON all register as <c>1</c>. A <c>.viu</c> buffer registers no language service at all —
/// deliberately, because that key stamps its own content type and re-breaks colorization — so the
/// GUID falls back to the default file type, the preferences come back zeroed, and every Viu view is
/// created with brace completion explicitly <b>off</b>. The pairs were always registered correctly;
/// the editor's manager returned before it ever consulted them.
/// </para>
/// <para>
/// <b>Why clearing and not writing <see langword="true"/>.</b> The option's own default is
/// <see langword="true"/> and nothing in Visual Studio writes the global scope, so the global value
/// <em>is</em> the user's Automatic Brace Completion choice.
/// <see cref="IEditorOptions.ClearOptionValue{T}(EditorOptionKey{T})"/> removes the spurious view
/// definition and lets that choice through; writing <see langword="true"/> would overwrite it and
/// take the off switch away from a user who had deliberately turned the feature off. The decision is
/// <see cref="ViuBraceCompletionEnablement.ShouldClearViewOverride"/> — pure and unit-tested — and it
/// declines in every state but the adapter's.
/// </para>
/// <para>
/// <b>Ordering, and why one pass is not enough.</b> The adapter's write happens inside
/// <c>Init_InitializeWpfTextView</c> <em>before</em> the WPF view exists, so it is always ahead of
/// any <see cref="ITextViewCreationListener"/> and clearing once at creation fixes the view the user
/// opens. It is not the only write: <c>SimpleTextViewWindow.OnUserPreferencesChanged7</c> calls
/// <c>AdoptLangPreferences</c> again whenever Tools &gt; Options broadcasts a change for that same
/// language-service GUID, which would re-disable a view already open. The
/// <see cref="IEditorOptions.OptionChanged"/> subscription below covers that, so correctness does not
/// depend on this part winning a race it has no way to enter.
/// </para>
/// <para>
/// <b>Loop safety.</b> Clearing raises <see cref="IEditorOptions.OptionChanged"/> synchronously and
/// re-enters this handler exactly once. By then the local definition is gone, so the pure condition
/// answers <see langword="false"/> and the recursion stops: the depth is bounded at two by the shape
/// of the condition rather than by a guard flag. A user toggling the global option raises the same
/// notification and is likewise declined — an inherited value is never cleared.
/// </para>
/// <para>
/// Runtime-only surface: the MEF export, the option reads, and the subscription are verified by
/// running the extension and by the opt-in trace this part writes; the decision behind them is
/// unit-tested.
/// </para>
/// </remarks>
[Export(typeof(ITextViewCreationListener))]
[ContentType(ViuContentTypes.Viu)]
[TextViewRole(PredefinedTextViewRoles.Document)]
[TextViewRole(PredefinedTextViewRoles.Editable)]
internal sealed class ViuBraceCompletionEnablementTextViewCreationListener : ITextViewCreationListener
{
    /// <inheritdoc />
    public void TextViewCreated(ITextView textView)
    {
        if (textView is null)
        {
            return;
        }

        RestoreInheritedBraceCompletion(textView, "view created");

        void OnOptionChanged(object sender, EditorOptionChangedEventArgs arguments)
        {
            if (string.Equals(
                    arguments.OptionId,
                    DefaultTextViewOptions.BraceCompletionEnabledOptionName,
                    StringComparison.Ordinal))
            {
                RestoreInheritedBraceCompletion(textView, "option changed");
            }
        }

        void OnClosed(object sender, EventArgs arguments)
        {
            textView.Options.OptionChanged -= OnOptionChanged;
            textView.Closed -= OnClosed;
        }

        textView.Options.OptionChanged += OnOptionChanged;
        textView.Closed += OnClosed;
    }

    private static void RestoreInheritedBraceCompletion(ITextView textView, string trigger)
    {
        try
        {
            IEditorOptions options = textView.Options;
            IEditorOptions? globalOptions = options.GlobalOptions;
            if (globalOptions is null)
            {
                // No global scope means no user choice to inherit, and clearing would resolve to the
                // definition's own default rather than to anything the user asked for. Decline.
                return;
            }

            bool definedOnThisView = options.IsOptionDefined(
                DefaultTextViewOptions.BraceCompletionEnabledOptionId,
                localScopeOnly: true);
            bool effectiveValue = options.GetOptionValue(
                DefaultTextViewOptions.BraceCompletionEnabledOptionId);
            bool globalValue = globalOptions.GetOptionValue(
                DefaultTextViewOptions.BraceCompletionEnabledOptionId);

            bool shouldClear = ViuBraceCompletionEnablement.ShouldClearViewOverride(
                definedOnThisView,
                effectiveValue,
                globalValue);

            if (shouldClear)
            {
                options.ClearOptionValue(DefaultTextViewOptions.BraceCompletionEnabledOptionId);
            }

            if (ViuEditorDiagnostics.IsEnabled)
            {
                TraceOutcome(options, globalOptions, trigger, definedOnThisView, effectiveValue, globalValue, shouldClear);
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            // An option the editor has torn down mid-close is the realistic case, and a view that is
            // going away needs no repair. Never worth a failed view creation or a throw out of an
            // option-changed notification, which the editor does not guard.
        }
    }

    private static void TraceOutcome(
        IEditorOptions options,
        IEditorOptions globalOptions,
        string trigger,
        bool definedOnThisView,
        bool effectiveValue,
        bool globalValue,
        bool cleared)
        => ViuEditorDiagnostics.Trace("brace.reenabled", () => string.Concat(
            "trigger=", trigger,
            " before(effective=", effectiveValue.ToString(),
            " definedOnThisView=", definedOnThisView.ToString(),
            " globalValue=", globalValue.ToString(), ")",
            " action=", cleared ? "cleared the view override" : "none",
            " after(effective=",
            options.GetOptionValue(DefaultTextViewOptions.BraceCompletionEnabledOptionId).ToString(),
            " definedOnThisView=",
            options.IsOptionDefined(
                DefaultTextViewOptions.BraceCompletionEnabledOptionId,
                localScopeOnly: true).ToString(),
            " globalValue=",
            globalOptions.GetOptionValue(
                DefaultTextViewOptions.BraceCompletionEnabledOptionId).ToString(),
            ")"));
}
