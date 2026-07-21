using System;
using System.Collections.Generic;

using Assimalign.Viu;
using Assimalign.Viu.Browser;

namespace Assimalign.Viu.TodoMvc.Components;

/// <summary>
/// The TodoMVC root — header, the todo list, and the footer, driven entirely by the shared
/// <see cref="TodoStore"/>. It injects the store (falling back to a fresh one so the component mounts
/// even without an app-level provide) and re-provides it to descendants. The <c>&lt;ul&gt;</c> renders
/// one <see cref="TodoItemComponent"/> per visible todo, keyed by <see cref="TodoItem.Id"/> so the
/// renderer reuses instances across filtering and reordering (Vue's keyed <c>v-for</c>). The whole
/// app spec is at https://github.com/tastejs/todomvc/blob/master/app-spec.md.
/// </summary>
public sealed class TodoAppComponent : IComponentDefinition
{
    // One shared child definition drives every row; per-row state lives on the instance the renderer
    // creates for each keyed vnode (see TodoItemComponent).
    private static readonly TodoItemComponent ItemView = new();

    /// <inheritdoc/>
    public string? Name => "TodoApp";

    /// <inheritdoc/>
    public Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context)
    {
        var store = DependencyInjection.Inject(TodoStore.Key) ?? new TodoStore();
        DependencyInjection.Provide(TodoStore.Key, store);

        var newTodo = Reactive.Reference(string.Empty);

        void AddFromInput()
        {
            store.Add(newTodo.Value);
            newTodo.Value = string.Empty;
        }

        VirtualNode Header() => VirtualNodeFactory.Element(
            "header",
            VirtualNodeFactory.Properties(("class", "header")),
            VirtualNodeFactory.Element("h1", "todos"),
            VirtualNodeFactory.Element(
                "input",
                VirtualNodeFactory.Properties(
                    ("class", "new-todo"),
                    ("placeholder", "What needs to be done?"),
                    ("value", newTodo.Value),
                    ("onInput", (Action<BrowserEvent>)(browserEvent => newTodo.Value = browserEvent.TargetValue ?? string.Empty)),
                    ("onKeyup", (Action<BrowserEvent>)(browserEvent =>
                    {
                        if (string.Equals(browserEvent.Key, "Enter", StringComparison.Ordinal))
                        {
                            AddFromInput();
                        }
                    }))),
                (VirtualNode?[]?)null));

        VirtualNode Main()
        {
            var visible = store.Visible.Value;
            var rows = new VirtualNode?[visible.Count];
            for (var index = 0; index < visible.Count; index++)
            {
                var todo = visible[index];
                rows[index] = VirtualNodeFactory.Component(
                    ItemView,
                    VirtualNodeFactory.Properties(("key", todo.Id), ("todo", todo)));
            }

            return VirtualNodeFactory.Element(
                "section",
                VirtualNodeFactory.Properties(("class", "main")),
                VirtualNodeFactory.Element(
                    "input",
                    VirtualNodeFactory.Properties(
                        ("id", "toggle-all"),
                        ("class", "toggle-all"),
                        ("type", "checkbox"),
                        ("checked", store.AllCompleted.Value),
                        ("onChange", (Action)(() => store.SetAll(!store.AllCompleted.Value)))),
                    (VirtualNode?[]?)null),
                VirtualNodeFactory.Element(
                    "label",
                    VirtualNodeFactory.Properties(("for", "toggle-all")),
                    "Mark all as complete"),
                VirtualNodeFactory.Element(
                    "ul",
                    VirtualNodeFactory.Properties(("class", "todo-list")),
                    rows));
        }

        VirtualNode FilterLink(string text, TodoFilter target) => VirtualNodeFactory.Element(
            "li",
            VirtualNodeFactory.Element(
                "a",
                VirtualNodeFactory.Properties(
                    ("class", store.Filter.Value == target ? "selected" : string.Empty),
                    ("href", "#"),
                    ("onClick", (Action)(() => store.SetFilter(target)))),
                text));

        VirtualNode Footer()
        {
            var remaining = store.RemainingCount.Value;
            return VirtualNodeFactory.Element(
                "footer",
                VirtualNodeFactory.Properties(("class", "footer")),
                VirtualNodeFactory.Element(
                    "span",
                    VirtualNodeFactory.Properties(("class", "todo-count")),
                    VirtualNodeFactory.Element("strong", remaining.ToString()),
                    VirtualNodeFactory.Text(remaining == 1 ? " item left" : " items left")),
                VirtualNodeFactory.Element(
                    "ul",
                    VirtualNodeFactory.Properties(("class", "filters")),
                    FilterLink("All", TodoFilter.All),
                    FilterLink("Active", TodoFilter.Active),
                    FilterLink("Completed", TodoFilter.Completed)),
                store.CompletedCount.Value > 0
                    ? VirtualNodeFactory.Element(
                        "button",
                        VirtualNodeFactory.Properties(
                            ("class", "clear-completed"),
                            ("type", "button"),
                            ("onClick", (Action)store.ClearCompleted)),
                        "Clear completed")
                    : null);
        }

        return () =>
        {
            var hasTodos = store.Items.Count > 0;
            return VirtualNodeFactory.Element(
                "section",
                VirtualNodeFactory.Properties(("class", "todoapp")),
                Header(),
                hasTodos ? Main() : null,
                hasTodos ? Footer() : null);
        };
    }
}
