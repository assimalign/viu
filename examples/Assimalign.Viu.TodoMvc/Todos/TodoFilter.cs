namespace Assimalign.Viu.TodoMvc;

/// <summary>
/// The active view filter — the three routes of the canonical TodoMVC footer
/// (https://github.com/tastejs/todomvc/blob/master/app-spec.md#routing). Kept as a plain enum held in
/// a <c>Reference&lt;TodoFilter&gt;</c> rather than wired to the router, so this sample stays focused
/// on reactivity and the component model.
/// </summary>
public enum TodoFilter
{
    /// <summary>Every todo, complete or not.</summary>
    All,

    /// <summary>Only todos that are not yet complete.</summary>
    Active,

    /// <summary>Only completed todos.</summary>
    Completed,
}
