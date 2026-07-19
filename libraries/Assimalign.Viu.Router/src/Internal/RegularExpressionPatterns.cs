using System.Text.RegularExpressions;

namespace Assimalign.Viu.Router;

/// <summary>
/// Compile-time regular expressions used while building path patterns. These patterns are constant
/// and known at build time, so they use <see cref="GeneratedRegexAttribute"/> (the Roslyn regex
/// source generator) — the AOT- and trimming-preferred path, which emits the matcher as source
/// rather than constructing it reflectively at runtime.
/// </summary>
internal static partial class RegularExpressionPatterns
{
    /// <summary>
    /// The characters that must be escaped when literal path text is embedded into a regular
    /// expression. The C# port of vue-router's <c>REGEX_CHARS_RE</c>
    /// (<c>packages/router/src/matcher/pathParserRanker.ts</c>): <c>. + * ? ^ $ { } ( ) [ ] / \</c>.
    /// </summary>
    [GeneratedRegex(@"[.+*?^${}()[\]/\\]")]
    public static partial Regex RegularExpressionSpecialCharacters();
}
