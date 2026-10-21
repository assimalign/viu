using Assimalign.Viu.Reactivity;

namespace EndToEndBrowserApp;

// Same generated reactive properties as the standalone consumer's task model.
[Reactive]
internal partial class TaskItem
{
    internal TaskItem(int id, string title)
    {
        Id = id;
        Title = title;
    }

    public int Id { get; }

    public partial string Title { get; set; }

    public partial bool IsDone { get; set; }
}
