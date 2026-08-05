using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text.BraceCompletion;
using Microsoft.VisualStudio.Utilities;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Registers the bracket pairs a <c>.viu</c> document completes in every section
/// ([V01.01.12.07.08]).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IBraceCompletionDefaultProvider"/> is metadata only: the part contributes no code, and
/// the editor's own brace-completion session handles insertion, type-through of the closing
/// character, and paired deletion on backspace. That is the right shape for these three pairs
/// precisely because they need no gating — braces, parentheses, and square brackets are balanced
/// constructs in template markup, in C#, and in CSS alike, so no position in a Viu container wants
/// them treated differently. Quotes do want that, and are registered separately by
/// <see cref="ViuQuoteBraceCompletionContextProvider"/>.
/// </para>
/// <para>
/// Interpolation falls out of the brace pair rather than needing a rule of its own: typing <c>{</c>
/// twice yields <c>{{}}</c> with the caret in the middle, which is the authoring path for
/// <c>{{ Count }}</c>.
/// </para>
/// <para>
/// The editor consults the user's Automatic Brace Completion option
/// (<c>DefaultTextViewOptions.BraceCompletionEnabledOptionId</c>) before it starts any session, so
/// this part neither reads nor overrides it: turning the option off turns these pairs off with it.
/// </para>
/// <para>
/// Runtime-only surface — this is MEF metadata that exists to be composed inside
/// <c>devenv.exe</c>, and it is verified by running the extension rather than by a unit test.
/// </para>
/// </remarks>
[Export(typeof(IBraceCompletionDefaultProvider))]
[BracePair('{', '}')]
[BracePair('(', ')')]
[BracePair('[', ']')]
[ContentType(ViuContentTypes.Viu)]
internal sealed class ViuBraceCompletionDefaults : IBraceCompletionDefaultProvider
{
}
