namespace Assimalign.Viu.Router;

/// <summary>
/// How a history navigation was initiated. The C# port of vue-router's <c>NavigationType</c>
/// (<c>packages/router/src/history/common.ts</c>): a <see cref="Push"/> is an application-initiated
/// <c>push</c>/<c>replace</c>, while a <see cref="Pop"/> is a browser-initiated back/forward
/// (a <c>popstate</c>) or the memory equivalent driven by <see cref="IRouterHistory.Go"/>.
/// </summary>
public enum NavigationType
{
    /// <summary>A browser back/forward or memory <c>go</c> (upstream <c>NavigationType.pop</c>, value <c>"pop"</c>).</summary>
    Pop,

    /// <summary>An application-initiated push/replace (upstream <c>NavigationType.push</c>, value <c>"push"</c>).</summary>
    Push,
}
