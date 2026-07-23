using System.Collections.Generic;

namespace Assimalign.Viu.Router;

/// <summary>
/// One entry in the matcher table: a <see cref="RouteRecord"/> paired with its compiled
/// <see cref="PathParser"/>, its parent link, and its child matchers. The C# port of vue-router's
/// <c>RouteRecordMatcher</c> (<c>packages/router/src/matcher/pathMatcher.ts</c>). The parent link
/// is what lets a resolved location report the full parent-to-child matched chain.
/// </summary>
internal sealed class RouteRecordMatcher
{
    public RouteRecordMatcher(RouteRecord record, string normalizedPath, PathParser parser, RouteRecordMatcher? parent)
    {
        Record = record;
        NormalizedPath = normalizedPath;
        Parser = parser;
        Parent = parent;
        Children = [];
    }

    /// <summary>The originating route record.</summary>
    public RouteRecord Record { get; }

    /// <summary>The fully resolved path (child paths joined onto their parent's path).</summary>
    public string NormalizedPath { get; }

    /// <summary>The compiled pattern used to match and interpolate this route.</summary>
    public PathParser Parser { get; }

    /// <summary>The parent matcher, or <see langword="null"/> for a top-level route.</summary>
    public RouteRecordMatcher? Parent { get; }

    /// <summary>The child matchers registered under this record.</summary>
    public List<RouteRecordMatcher> Children { get; }
}
