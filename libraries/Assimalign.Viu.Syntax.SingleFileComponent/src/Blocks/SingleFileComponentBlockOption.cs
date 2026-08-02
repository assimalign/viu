namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// A single option or attribute on a block header, such as <c>scoped</c> or <c>lang="scss"</c>.
/// Represents a tag attribute (<c>&lt;style scoped&gt;</c> — the canonical <c>.viu</c> template/style
/// form since [V01.01.06.10], and every <c>.vue</c> block) and an @-header option
/// (<c>@script lang="csharp" {</c>, custom and legacy blocks) with one identical record — the two
/// grammars surface the same name/value pairs. See the Vue SFC block-attribute contract at
/// https://vuejs.org/api/sfc-spec.html. Immutable and value-equatable so identical headers
/// compare equal.
/// </summary>
/// <param name="Name">The option name (e.g. <c>scoped</c>, <c>lang</c>, <c>module</c>).</param>
/// <param name="Value">The authored value without delimiters, or <see langword="null"/> for a valueless flag.</param>
/// <param name="Location">The source range covering the whole option token.</param>
public sealed record SingleFileComponentBlockOption(string Name, string? Value, SourceLocation Location);
