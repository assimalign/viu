using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.TodoMvc;

/// <summary>
/// A single todo — a source-generated <c>[Reactive]</c> object (Vue 3.5's <c>reactive()</c>,
/// https://vuejs.org/api/reactivity-core.html#reactive). The <c>Assimalign.Viu.Reactivity.Generators</c>
/// generator fills in each <c>partial</c> property with track-on-get / trigger-on-change plumbing, so
/// toggling <see cref="Completed"/> or renaming <see cref="Title"/> re-renders exactly the effects
/// that read that member — no JavaScript <c>Proxy</c>, no reflection. Held in the store's
/// <see cref="ReactiveList{T}"/>.
/// </summary>
[Reactive]
public partial class TodoItem
{
    /// <summary>The stable identity used as the keyed-list key (never reused within a session).</summary>
    public partial int Id { get; set; }

    /// <summary>The todo's text.</summary>
    public partial string Title { get; set; }

    /// <summary>Whether the todo is done.</summary>
    public partial bool Completed { get; set; }
}
