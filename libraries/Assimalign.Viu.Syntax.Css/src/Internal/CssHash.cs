using System.Globalization;

namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// The deterministic FNV-1a hash the CSS Modules class-name rewrite (<see cref="CssModuleRewriter"/>) and
/// the <c>v-bind()</c> custom-property rewrite (<see cref="CssBindingRewriter"/>) derive their local,
/// component-scoped names from. It is the same FNV-1a scheme the generator's scope id uses
/// (<c>Assimalign.Viu.Tooling.Css.StyleScopeId</c>, [V01.01.06.04]) so all three families of hash
/// are consistent: culture-free, stable across machines and rebuilds (the asset-caching contract), and
/// eight lowercase hex digits. String-only (no <c>System.IO</c>), so it stays inside the analyzer API
/// surface (RS1035).
/// </summary>
/// <remarks>
/// The caller salts the input with the component's short scope id (the <c>data-v-</c> hash), so the same
/// class name or expression in two different components hashes differently — the per-component uniqueness
/// the CSS Modules and <c>v-bind()</c> acceptance criteria require, mirroring Vue's <c>compileStyle</c>
/// modules hashing and <c>cssVars.ts</c> <c>genVarName</c> both folding the file id into the hash.
/// </remarks>
internal static class CssHash
{
    /// <summary>Computes the eight-hex-digit FNV-1a hash of <paramref name="value"/>.</summary>
    /// <param name="value">The already-salted string to hash.</param>
    /// <returns>The eight lowercase hex digits.</returns>
    public static string Compute(string value)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in value)
            {
                hash = (hash ^ character) * 16777619u;
            }

            return hash.ToString("x8", CultureInfo.InvariantCulture);
        }
    }
}
