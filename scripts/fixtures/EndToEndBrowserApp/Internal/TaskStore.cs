using System;
using System.Linq;

using Assimalign.Viu.Reactivity;
using Assimalign.Viu.State;

namespace EndToEndBrowserApp;

// Mirror the standalone consumer's shared store, with a deterministic single-row seed.
internal sealed class TaskStore
{
    public static StateStoreDefinition<TaskStore> Definition { get; } = new(
        "tasks", static _ => new TaskStore());

    private int _nextIdentifier = 1;

    private TaskStore()
    {
        Add("Initial task");
        Remaining = Reactive.Computed(() => Items.Count(item => !item.IsDone));
        Completed = Reactive.Computed(() => Items.Count - Remaining.Value);
    }

    public ReactiveList<TaskItem> Items { get; } = new();

    public Computed<int> Remaining { get; }

    public Computed<int> Completed { get; }

    public void Add(string title)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            Items.Add(new TaskItem(_nextIdentifier++, title.Trim()));
        }
    }

    public void Remove(TaskItem item) => Items.Remove(item);

    public void ClearCompleted()
    {
        using (Reactive.Batch())
        {
            for (int index = Items.Count - 1; index >= 0; index--)
            {
                if (Items[index].IsDone)
                {
                    Items.RemoveAt(index);
                }
            }
        }
    }
}
