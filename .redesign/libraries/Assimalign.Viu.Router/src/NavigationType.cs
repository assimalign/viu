namespace Assimalign.Viu.Router;

/// <summary>
/// How a history navigation was initiated: a <see cref="Push"/> comes from the application calling
/// <see cref="IRouterHistory.Push"/>/<see cref="IRouterHistory.Replace"/>, while a <see cref="Pop"/>
/// comes from a browser back/forward (a <c>popstate</c>) or the memory equivalent driven by
/// <see cref="IRouterHistory.Go"/>.
/// </summary>
/// <remarks>Specified by <c>[RTR-3]</c>.</remarks>
public enum NavigationType
{
    /// <summary>A browser back/forward, or the memory equivalent of one.</summary>
    Pop,

    /// <summary>An application-initiated push or replace.</summary>
    Push,
}
