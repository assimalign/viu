using System;

namespace Assimalign.Viu.Router;

/// <summary>
/// The pure base-path arithmetic shared by the web and hash histories: normalize a configured base,
/// strip it off a read location, prepend it (or reduce it to the leading <c>#</c>) for an
/// <c>href</c>, compute the hash-mode base, and derive the current base-stripped location from raw
/// URL components.
/// </summary>
/// <remarks>
/// Every method is a total function of its string inputs — no DOM, no interop — so base handling is
/// unit-testable in a plain .NET host. The browser edge supplies the raw URL components
/// (<c>location.pathname</c>/<c>search</c>/<c>hash</c>/<c>host</c>) and this class does the policy.
/// </remarks>
internal static class HistoryPathNormalization
{
    /// <summary>
    /// Removes a single trailing <c>/</c>, leaving an already slash-free path unchanged.
    /// </summary>
    internal static string RemoveTrailingSlash(string path)
        => path.Length > 0 && path[^1] == '/' ? path[..^1] : path;

    /// <summary>
    /// Normalizes a configured base: defaults an empty base to <c>"/"</c>, forces a leading <c>/</c>
    /// unless it already starts with <c>/</c> or <c>#</c>, then trims a trailing slash — so
    /// <c>"/app/"</c> and <c>"app"</c> both become <c>"/app"</c> and <c>"/"</c> becomes <c>""</c>
    /// (the "no base" sentinel). The browser <c>&lt;base&gt;</c> href default is resolved by the
    /// caller (see <see cref="StripBaseHrefOrigin"/>) before this.
    /// </summary>
    /// <param name="rawBase">The configured base, or <see langword="null"/>/empty for the default.</param>
    internal static string NormalizeBase(string? rawBase)
    {
        var value = string.IsNullOrEmpty(rawBase) ? "/" : rawBase;
        if (value[0] != '/' && value[0] != '#')
        {
            value = "/" + value;
        }
        return RemoveTrailingSlash(value);
    }

    /// <summary>
    /// Strips a leading <c>scheme://host</c> from a <c>&lt;base href&gt;</c> so only the path portion
    /// remains. Implemented as a scan rather than a regular expression, so the base path is
    /// resolvable without pulling the regex engine into a trimmed application.
    /// </summary>
    /// <param name="href">The raw <c>&lt;base&gt;</c> href.</param>
    internal static string StripBaseHrefOrigin(string href)
    {
        var schemeIndex = href.IndexOf("://", StringComparison.Ordinal);
        if (schemeIndex <= 0 || !IsScheme(href.AsSpan(0, schemeIndex)))
        {
            return href;
        }
        var hostStart = schemeIndex + 3;
        var pathSlash = href.IndexOf('/', hostStart);
        return pathSlash >= 0 ? href[pathSlash..] : string.Empty;
    }

    /// <summary>
    /// Strips the base off the beginning of a read pathname, case-insensitively, collapsing an
    /// exact-base match to <c>"/"</c>, so a visit to the base itself resolves to the root route.
    /// </summary>
    /// <param name="pathname">The raw <c>location.pathname</c>.</param>
    /// <param name="base">The normalized base (empty means "no base").</param>
    internal static string StripBase(string pathname, string @base)
    {
        if (string.IsNullOrEmpty(@base)
            || !pathname.StartsWith(@base, StringComparison.OrdinalIgnoreCase))
        {
            return pathname;
        }
        var stripped = pathname[@base.Length..];
        return stripped.Length == 0 ? "/" : stripped;
    }

    /// <summary>
    /// Builds an <c>href</c> value: <c>base + location</c> for a plain base, or (when the base carries
    /// a <c>#</c> after at least one leading character, i.e. hash mode) the leading <c>#</c> plus the
    /// location — in hash mode everything before the <c>#</c> is dropped, because the server never
    /// sees the fragment and the anchor must stay same-document.
    /// </summary>
    /// <param name="base">The normalized base.</param>
    /// <param name="location">The base-stripped location.</param>
    internal static string CreateHref(string @base, string location)
    {
        // BEFORE_HASH_RE requires >= 1 non-'#' char before the first '#'; only then is everything up
        // to and including that '#' collapsed to a single '#'. A leading '#' (index 0) or no '#'
        // leaves the base verbatim.
        var hashIndex = @base.IndexOf('#');
        var prefix = hashIndex > 0 ? "#" : @base;
        return prefix + location;
    }

    /// <summary>
    /// Computes the hash-mode base from the current URL: the provided base (or
    /// <c>pathname + search</c>) when a host is present, or empty for a hostless <c>file://</c> URL,
    /// with a <c>#</c> appended if absent. The result is then passed through
    /// <see cref="NormalizeBase"/>, so hash mode and web mode share one base representation.
    /// </summary>
    /// <param name="providedBase">The caller-supplied base, or <see langword="null"/>.</param>
    /// <param name="host">The raw <c>location.host</c> (empty for <c>file://</c>).</param>
    /// <param name="pathname">The raw <c>location.pathname</c>.</param>
    /// <param name="search">The raw <c>location.search</c>.</param>
    internal static string ComputeHashBase(string? providedBase, string host, string pathname, string search)
    {
        string result;
        if (!string.IsNullOrEmpty(host))
        {
            result = string.IsNullOrEmpty(providedBase) ? pathname + search : providedBase;
        }
        else
        {
            result = string.Empty;
        }
        if (!result.Contains('#', StringComparison.Ordinal))
        {
            result += "#";
        }
        return result;
    }

    /// <summary>
    /// Derives the current base-stripped location from raw URL components. In hash mode (base carries
    /// a <c>#</c>) the location is the portion of the fragment after the hash-base, forced to a
    /// leading <c>/</c>; otherwise it is the base-stripped pathname plus the search and hash.
    /// </summary>
    /// <param name="base">The normalized base.</param>
    /// <param name="pathname">The raw <c>location.pathname</c>.</param>
    /// <param name="search">The raw <c>location.search</c> (including a leading <c>?</c> when present).</param>
    /// <param name="hash">The raw <c>location.hash</c> (including a leading <c>#</c> when present).</param>
    internal static string CreateCurrentLocation(string @base, string pathname, string search, string hash)
    {
        var hashPosition = @base.IndexOf('#');
        if (hashPosition >= 0)
        {
            var hashBase = @base[hashPosition..];
            var slicePosition = hash.Contains(hashBase, StringComparison.Ordinal) ? hashBase.Length : 1;
            var pathFromHash = hash.Length > slicePosition ? hash[slicePosition..] : string.Empty;
            if (pathFromHash.Length == 0 || pathFromHash[0] != '/')
            {
                pathFromHash = "/" + pathFromHash;
            }
            // The hash base has already been sliced off, so there is nothing further to strip.
            return pathFromHash;
        }
        return StripBase(pathname, @base) + search + hash;
    }

    private static bool IsScheme(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (!char.IsLetterOrDigit(character))
            {
                return false;
            }
        }
        return value.Length > 0;
    }
}
