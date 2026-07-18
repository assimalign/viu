namespace Assimalign.Vue.Syntax.SingleFileComponent;

/// <summary>
/// A single option on a block header, written between the block name and the opening brace, e.g.
/// <c>@style scoped module="classes" lang="scss" {</c>. The <c>.viu</c> equivalent of a Vue SFC block
/// attribute (https://vuejs.org/api/sfc-spec.html): <c>scoped</c> is a valueless flag and
/// <c>lang="scss"</c> a double-quoted key/value. Immutable and value-equatable so identical headers
/// compare equal.
/// </summary>
/// <param name="Name">The option name (e.g. <c>scoped</c>, <c>lang</c>, <c>module</c>).</param>
/// <param name="Value">The double-quoted value, or <see langword="null"/> for a valueless flag.</param>
/// <param name="Location">The source range covering the whole option token.</param>
public sealed record SingleFileComponentBlockOption(string Name, string? Value, SourceLocation Location);
