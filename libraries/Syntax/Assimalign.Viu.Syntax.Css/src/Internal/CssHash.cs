using System.Globalization;

namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// Computes deterministic FNV-1a hexadecimal hashes for CSS module class names and CSS binding
/// custom properties. Callers salt each input with the component path hash so equal local names
/// in different components remain distinct. The algorithm and spelling are stable across rebuilds
/// and are retained by <c>[V01.01.06.17]</c>.
/// </summary>
/// <remarks>The value is a CSS name suffix only; it does not identify element attributes.</remarks>
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
