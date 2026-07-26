namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// A single option or attribute on a block header, such as <c>scoped</c> or <c>lang="scss"</c>.
/// Represents both an option in Viu's canonical <c>@style scoped {</c> form and an attribute in the
/// [V01.01.06.09] compatibility <c>&lt;style scoped&gt;</c> form. See the Vue SFC block-attribute
/// contract at https://vuejs.org/api/sfc-spec.html. Immutable and value-equatable so identical headers
/// compare equal.
/// </summary>
/// <param name="Name">The option name (e.g. <c>scoped</c>, <c>lang</c>, <c>module</c>).</param>
/// <param name="Value">The authored value without delimiters, or <see langword="null"/> for a valueless flag.</param>
/// <param name="Location">The source range covering the whole option token.</param>
public sealed record SingleFileComponentBlockOption(string Name, string? Value, SourceLocation Location);
