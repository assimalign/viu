using System.Collections.Generic;
using System.Linq;

using Assimalign.Viu.Reactivity;
using Assimalign.Viu.RuntimeCore;

namespace Assimalign.Viu.TodoMvc;

/// <summary>
/// The TodoMVC application state, written as a composition-style store: a <see cref="ReactiveList{T}"/>
/// of <see cref="TodoItem"/> plus a handful of <c>Computed</c> views and the actions that mutate them.
/// This is the whole "model" of the app and the primary unit under test — it depends only on
/// <c>Assimalign.Viu.Reactivity</c>, so the sibling test project drives it with no browser.
/// <para>
/// It is shared across components through the typed <see cref="Key"/> (Vue's <c>InjectionKey</c>):
/// the bootstrap <c>provide</c>s one instance app-wide and every component <c>inject</c>s it, mirroring
/// the reactivity-store pattern in
/// https://vuejs.org/guide/scaling-up/state-management.html#simple-state-management-with-reactivity-api.
/// </para>
/// </summary>
public sealed class TodoStore
{
    /// <summary>The typed provide/inject key the bootstrap and every component share.</summary>
    public static readonly InjectionKey<TodoStore> Key = new("todo-store");

    private readonly ReactiveList<TodoItem> _items = [];
    private readonly Reference<TodoFilter> _filter = Reactive.Reference(TodoFilter.All);
    private int _nextId;

    /// <summary>Creates an empty store.</summary>
    public TodoStore()
    {
        // Computeds are lazy + versioned (Vue's computed()): each recomputes only when a dependency it
        // read last time changes, so reading them per render is cheap. Their run counts are pinned by
        // the tests, which is where reactivity bugs actually hide.
        Visible = Reactive.Computed<IReadOnlyList<TodoItem>>(() => _filter.Value switch
        {
            TodoFilter.Active => _items.Where(item => !item.Completed).ToArray(),
            TodoFilter.Completed => _items.Where(item => item.Completed).ToArray(),
            _ => _items.ToArray(),
        });
        RemainingCount = Reactive.Computed(() => _items.Count(item => !item.Completed));
        CompletedCount = Reactive.Computed(() => _items.Count(item => item.Completed));
        AllCompleted = Reactive.Computed(() => _items.Count > 0 && _items.All(item => item.Completed));
    }

    /// <summary>The live backing collection (every mutation flows through the actions below).</summary>
    public ReactiveList<TodoItem> Items => _items;

    /// <summary>The active view filter (settable through <see cref="SetFilter"/>).</summary>
    public Reference<TodoFilter> Filter => _filter;

    /// <summary>The todos matching <see cref="Filter"/>, in list order (Vue's <c>filteredTodos</c>).</summary>
    public Computed<IReadOnlyList<TodoItem>> Visible { get; }

    /// <summary>The number of not-yet-complete todos (the footer's "items left").</summary>
    public Computed<int> RemainingCount { get; }

    /// <summary>The number of completed todos (drives the "Clear completed" affordance).</summary>
    public Computed<int> CompletedCount { get; }

    /// <summary>Whether there is at least one todo and all todos are complete (the toggle-all state).</summary>
    public Computed<bool> AllCompleted { get; }

    /// <summary>
    /// Adds a todo from <paramref name="title"/> after trimming; blank input is ignored (app-spec: "If
    /// it's empty the input is not added"). Returns the created item, or <see langword="null"/> when
    /// the trimmed title was empty.
    /// </summary>
    /// <param name="title">The raw input text.</param>
    /// <returns>The new <see cref="TodoItem"/>, or <see langword="null"/> when nothing was added.</returns>
    public TodoItem? Add(string? title)
    {
        var trimmed = title?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }
        var item = new TodoItem { Id = ++_nextId, Title = trimmed, Completed = false };
        _items.Add(item);
        return item;
    }

    /// <summary>Removes <paramref name="item"/> (the todo's destroy button).</summary>
    /// <param name="item">The todo to remove.</param>
    public void Remove(TodoItem item) => _items.Remove(item);

    /// <summary>Flips <paramref name="item"/>'s completed state (the item checkbox).</summary>
    /// <param name="item">The todo to toggle.</param>
    public void Toggle(TodoItem item) => item.Completed = !item.Completed;

    /// <summary>Sets every todo's completed state to <paramref name="completed"/> (the toggle-all control).</summary>
    /// <param name="completed">The state to apply to all todos.</param>
    public void SetAll(bool completed)
    {
        foreach (var item in _items)
        {
            item.Completed = completed;
        }
    }

    /// <summary>Removes every completed todo (the footer's "Clear completed").</summary>
    public void ClearCompleted()
    {
        foreach (var completed in _items.Where(item => item.Completed).ToArray())
        {
            _items.Remove(completed);
        }
    }

    /// <summary>
    /// Renames <paramref name="item"/> to a trimmed <paramref name="title"/>; an empty edit removes the
    /// todo (app-spec: "If the text is empty the todo should instead be destroyed").
    /// </summary>
    /// <param name="item">The todo being edited.</param>
    /// <param name="title">The new raw text.</param>
    public void Rename(TodoItem item, string? title)
    {
        var trimmed = title?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            _items.Remove(item);
            return;
        }
        item.Title = trimmed;
    }

    /// <summary>Selects the active view filter.</summary>
    /// <param name="filter">The filter to activate.</param>
    public void SetFilter(TodoFilter filter) => _filter.Value = filter;

    /// <summary>Creates a store pre-populated with a couple of demo todos for first load.</summary>
    /// <returns>A seeded store.</returns>
    public static TodoStore CreateSeeded()
    {
        var store = new TodoStore();
        store.Add("Learn Viu's reactive core");
        var done = store.Add("Read the .viu single-file-component format");
        store.Add("Ship a component to the browser");
        if (done is not null)
        {
            done.Completed = true;
        }
        return store;
    }
}
