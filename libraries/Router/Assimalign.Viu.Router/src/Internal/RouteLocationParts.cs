using System;

namespace Assimalign.Viu.Router;

/// <summary>Splits a base-stripped location once before matching, preserving suffix spelling [RTR-12].</summary>
internal readonly struct RouteLocationParts
{
    private RouteLocationParts(string path, RouteQuery query, string fragment, bool hasQuery, bool hasFragment)
    {
        Path = path;
        Query = query;
        Fragment = fragment;
        HasQuery = hasQuery;
        HasFragment = hasFragment;
    }

    internal string Path { get; }
    internal RouteQuery Query { get; }
    internal string Fragment { get; }
    internal bool HasQuery { get; }
    internal bool HasFragment { get; }

    internal static RouteLocationParts Parse(string location)
    {
        ReadOnlySpan<char> text = location.AsSpan();
        int fragmentPosition = text.IndexOf('#');
        ReadOnlySpan<char> beforeFragment = fragmentPosition < 0 ? text : text[..fragmentPosition];
        int queryPosition = beforeFragment.IndexOf('?');
        int pathLength = queryPosition < 0 ? beforeFragment.Length : queryPosition;
        return new RouteLocationParts(
            pathLength == location.Length ? location : location[..pathLength],
            queryPosition < 0 ? RouteQuery.Empty : RouteQuery.Parse(beforeFragment[(queryPosition + 1)..].ToString()),
            fragmentPosition < 0 ? string.Empty : location[(fragmentPosition + 1)..],
            queryPosition >= 0,
            fragmentPosition >= 0);
    }
}
