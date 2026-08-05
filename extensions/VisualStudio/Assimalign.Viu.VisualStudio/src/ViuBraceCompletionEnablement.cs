namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Decides whether a <c>.viu</c> view's Automatic Brace Completion option carries an override that
/// should be removed ([V01.01.12.07.09]).
/// </summary>
/// <remarks>
/// <para>
/// The override is not the user's. Visual Studio's legacy editor adapter copies the buffer's
/// <em>language service</em> preferences onto every view it creates
/// (<c>SimpleTextViewWindow.AdoptLangPreferences</c>), and a <c>.viu</c> buffer has no language
/// service to read them from, so it inherits a zeroed <c>LANGPREFERENCES</c> and the option is
/// written <see langword="false"/> — see <c>docs/DESIGN.md</c> for the full trail.
/// </para>
/// <para>
/// <b>Why the answer is three inputs and not one.</b> Removing the override must not become a way to
/// force the feature on. Clearing is correct only when the view carries its own definition, that
/// definition disables the feature, and the global scope — the only place the user's own Automatic
/// Brace Completion choice lives — has it enabled. If the user turned brace completion off globally,
/// the view's <see langword="false"/> agrees with them and stays.
/// </para>
/// <para>
/// Pure and editor-free, so every combination is pinned by unit tests rather than by reasoning about
/// an editor the tests cannot host.
/// </para>
/// </remarks>
internal static class ViuBraceCompletionEnablement
{
    /// <summary>
    /// Determines whether the view-scoped Automatic Brace Completion value should be cleared so the
    /// view inherits the global one again.
    /// </summary>
    /// <param name="definedOnThisView">
    /// Whether the option is defined in the view's own scope rather than inherited.
    /// </param>
    /// <param name="effectiveValue">The value the view currently resolves.</param>
    /// <param name="globalValue">The value the global scope holds — the user's own choice.</param>
    /// <returns>
    /// <see langword="true"/> only for the one state the adapter leaves behind: a locally defined
    /// value that is off while the user's global choice is on.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The condition is also what makes the fix loop-proof. Clearing the value removes the local
    /// definition, so re-asking immediately afterwards — which is exactly what the option-changed
    /// notification raised by the clear does — answers <see langword="false"/> on
    /// <paramref name="definedOnThisView"/> and stops. There is no state in which clearing leaves the
    /// condition true.
    /// </para>
    /// <para>
    /// The three other reasons to decline are each real: an inherited value is already what the user
    /// asked for; a locally defined <see langword="true"/> is somebody deliberately enabling the
    /// feature and needs no help; and a global <see langword="false"/> is the user's off switch, which
    /// this must never override.
    /// </para>
    /// </remarks>
    public static bool ShouldClearViewOverride(
        bool definedOnThisView,
        bool effectiveValue,
        bool globalValue)
        => definedOnThisView && !effectiveValue && globalValue;
}
