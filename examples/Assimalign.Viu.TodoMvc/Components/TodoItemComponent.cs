using System;
using System.Collections.Generic;

using Assimalign.Viu.Reactivity;
using Assimalign.Viu.RuntimeCore;
using Assimalign.Viu.RuntimeDom;

namespace Assimalign.Viu.TodoMvc.Components;

/// <summary>
/// One row of the todo list — a child component fed its <c>todo</c> by prop and reaching the shared
/// <see cref="TodoStore"/> through inject. Its edit state (<c>editing</c>/<c>draft</c>) is local
/// per-instance <c>ref</c> state created in <see cref="Setup"/>, so a single shared definition renders
/// every row while each mounted instance keeps its own editing state (Vue's keyed-list reuse:
/// https://vuejs.org/guide/essentials/list.html#maintaining-state-with-key). Toggle, destroy, and
/// begin-edit are no-argument handlers so the in-memory test renderer can drive them; the two text
/// inputs read the typed <see cref="BrowserEvent"/> payload and so run only in the browser.
/// </summary>
public sealed class TodoItemComponent : IComponentDefinition
{
    private static readonly IReadOnlyList<ComponentPropertyDefinition> DeclaredProperties =
        [new ComponentPropertyDefinition("todo")];

    /// <inheritdoc/>
    public string? Name => "TodoItem";

    /// <inheritdoc/>
    public IReadOnlyList<ComponentPropertyDefinition>? Properties => DeclaredProperties;

    /// <inheritdoc/>
    public Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context)
    {
        // The todo reference is stable for the life of this keyed instance, so capturing it once is
        // correct; its reactive members (read in the render below) drive re-renders on their own.
        var todo = properties.Get<TodoItem>("todo")!;
        var store = DependencyInjection.Inject(TodoStore.Key)!;

        var editing = Reactive.Reference(false);
        var draft = Reactive.Reference(string.Empty);

        void BeginEdit()
        {
            draft.Value = todo.Title;
            editing.Value = true;
        }

        void Commit()
        {
            if (!editing.Value)
            {
                return;
            }
            editing.Value = false;
            store.Rename(todo, draft.Value);
        }

        void Cancel() => editing.Value = false;

        return () =>
        {
            var rowClass = "todo-item"
                + (todo.Completed ? " is-completed" : string.Empty)
                + (editing.Value ? " is-editing" : string.Empty);

            var view = VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.Properties(("class", "todo-view")),
                VirtualNodeFactory.Element(
                    "input",
                    VirtualNodeFactory.Properties(
                        ("class", "toggle"),
                        ("type", "checkbox"),
                        ("checked", todo.Completed),
                        ("onChange", (Action)(() => store.Toggle(todo)))),
                    (VirtualNode?[]?)null),
                VirtualNodeFactory.Element(
                    "label",
                    VirtualNodeFactory.Properties(("onDblclick", (Action)BeginEdit)),
                    todo.Title),
                VirtualNodeFactory.Element(
                    "button",
                    VirtualNodeFactory.Properties(
                        ("class", "destroy"),
                        ("type", "button"),
                        ("aria-label", "Delete todo"),
                        ("onClick", (Action)(() => store.Remove(todo)))),
                    "×"));

            VirtualNode? editor = editing.Value
                ? VirtualNodeFactory.Element(
                    "input",
                    VirtualNodeFactory.Properties(
                        ("class", "edit"),
                        ("value", draft.Value),
                        ("onInput", (Action<BrowserEvent>)(browserEvent => draft.Value = browserEvent.TargetValue ?? string.Empty)),
                        ("onBlur", (Action)Commit),
                        ("onKeyup", (Action<BrowserEvent>)(browserEvent =>
                        {
                            if (string.Equals(browserEvent.Key, "Enter", StringComparison.Ordinal))
                            {
                                Commit();
                            }
                            else if (string.Equals(browserEvent.Key, "Escape", StringComparison.Ordinal))
                            {
                                Cancel();
                            }
                        }))),
                    (VirtualNode?[]?)null)
                : null;

            return VirtualNodeFactory.Element(
                "li",
                VirtualNodeFactory.Properties(("class", rowClass)),
                view,
                editor);
        };
    }
}
