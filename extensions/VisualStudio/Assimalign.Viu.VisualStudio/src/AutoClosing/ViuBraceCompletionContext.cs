using Microsoft.VisualStudio.Text.BraceCompletion;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// The language-specific half of a Viu quote-completion session: nothing beyond the editor's default
/// behavior ([V01.01.12.07.08]).
/// </summary>
/// <remarks>
/// <para>
/// A context exists at all only because the decision to <em>start</em> a quote session is
/// position-dependent, and <see cref="IBraceCompletionContextProvider"/> is the extension point that
/// can decline. Once a session has started there is nothing Viu wants to add: the editor already
/// inserts the pair, tracks the span, types through the closing quote, and deletes the pair on
/// backspace, and none of that is language-specific for a quoted string.
/// </para>
/// <para>
/// One instance serves every session because the type holds no state; the session it is handed is
/// passed in on each call.
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
    public void OnReturn(IBraceCompletionSession session)
    {
    }

    /// <inheritdoc />
    /// <remarks>
    /// Typing the closing quote inside the session always moves through it rather than inserting a
    /// second one: the editor only asks when there is nothing but whitespace between the caret and
    /// the closing quote, so the only way to reach this question is for the user to be finishing the
    /// string the session opened.
    /// </remarks>
    public bool AllowOverType(IBraceCompletionSession session) => true;
}
