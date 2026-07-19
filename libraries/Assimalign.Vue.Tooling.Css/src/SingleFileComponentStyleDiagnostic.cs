using Assimalign.Vue.Syntax;

namespace Assimalign.Vue.Tooling.Css;

/// <summary>
/// A recoverable diagnostic surfaced by <see cref="SingleFileComponentStyleCompiler"/> from a
/// <c>@style</c>-block rewrite (today: a malformed <c>v-bind()</c> — an unterminated <c>v-bind(</c> or an
/// empty <c>v-bind()</c> — [V01.01.06.06]). It carries the base <see cref="Diagnostic"/> exactly as the CSS
/// rewriter reported it (positions relative to the <c>@style</c> block's content) together with
/// <see cref="BlockContentStart"/>, the file position where that block's content begins. The generator host
/// composes the two into <c>.viu</c> file coordinates through its diagnostic envelope; the
/// <c>VuecsBundleCss</c> task host does not report these (the generator already does — the task is the
/// no-diagnostics I/O sibling), so it discards them. Keeping the block start alongside the diagnostic means
/// the core owns no host-specific coordinate mapping.
/// </summary>
/// <param name="Diagnostic">The base CSS rewriter diagnostic, positioned relative to the block content.</param>
/// <param name="BlockContentStart">The file position where the originating <c>@style</c> block's content begins.</param>
public sealed record SingleFileComponentStyleDiagnostic(Diagnostic Diagnostic, Position BlockContentStart);
