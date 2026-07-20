using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Testing;
using Assimalign.Viu.TodoMvc.Components;

namespace Assimalign.Viu.TodoMvc.Tests;

// Drives the real component tree through the in-memory Assimalign.Viu.Testing renderer (no browser):
// the store is provided at mount, and the no-argument DOM handlers (toggle, destroy, toggle-all,
// filter, clear-completed, begin/blur edit) are exercised through trigger(). The two text inputs read
// a BrowserEvent payload and so are covered in the browser instead; here they are seeded via the store.
public sealed class TodoAppComponentTests
{
    private static (ComponentWrapper Wrapper, TodoStore Store) MountWithTodos(params string[] titles)
    {
        var store = new TodoStore();
        foreach (var title in titles)
        {
            store.Add(title);
        }
        var options = new ComponentMountOptions().Provide(TodoStore.Key, store);
        return (ViuTest.Mount(new TodoAppComponent(), options), store);
    }

    [Fact]
    public void Mount_RendersOneRowPerTodo_AndTheItemsLeftCount()
    {
        var (wrapper, _) = MountWithTodos("a", "b");
        using (wrapper)
        {
            wrapper.FindAll(".todo-item").Count.ShouldBe(2);
            wrapper.Get(".todo-count").Text().ShouldBe("2 items left");
        }
    }

    [Fact]
    public void EmptyStore_HidesTheMainAndFooterSections()
    {
        var (wrapper, _) = MountWithTodos();
        using (wrapper)
        {
            wrapper.Find(".main").ShouldBeNull();
            wrapper.Find(".footer").ShouldBeNull();
        }
    }

    [Fact]
    public async Task TogglingAnItem_CompletesIt_AndUpdatesTheCount()
    {
        var (wrapper, store) = MountWithTodos("a", "b");
        using (wrapper)
        {
            await wrapper.FindAll(".toggle")[0].Trigger("change");

            store.Items[0].Completed.ShouldBeTrue();
            wrapper.FindAll(".is-completed").Count.ShouldBe(1);
            wrapper.Get(".todo-count").Text().ShouldBe("1 item left");
        }
    }

    [Fact]
    public async Task DestroyButton_RemovesTheRow()
    {
        var (wrapper, store) = MountWithTodos("a", "b");
        using (wrapper)
        {
            await wrapper.FindAll(".destroy")[0].Trigger("click");

            store.Items.Count.ShouldBe(1);
            wrapper.FindAll(".todo-item").Count.ShouldBe(1);
        }
    }

    [Fact]
    public async Task ToggleAll_CompletesEveryTodo()
    {
        var (wrapper, store) = MountWithTodos("a", "b", "c");
        using (wrapper)
        {
            await wrapper.Get(".toggle-all").Trigger("change");

            store.AllCompleted.Value.ShouldBeTrue();
            wrapper.FindAll(".is-completed").Count.ShouldBe(3);
            wrapper.Get(".todo-count").Text().ShouldBe("0 items left");
        }
    }

    [Fact]
    public async Task Filters_NarrowTheVisibleRows()
    {
        var (wrapper, _) = MountWithTodos("a", "b");
        using (wrapper)
        {
            // Complete the first row, then click the "Active" filter (the second of the three links).
            await wrapper.FindAll(".toggle")[0].Trigger("change");
            await wrapper.FindAll("a")[1].Trigger("click");

            wrapper.FindAll(".todo-item").Count.ShouldBe(1);

            // "Completed" (third link) shows the other one.
            await wrapper.FindAll("a")[2].Trigger("click");
            wrapper.FindAll(".todo-item").Count.ShouldBe(1);
            wrapper.FindAll(".is-completed").Count.ShouldBe(1);
        }
    }

    [Fact]
    public async Task ClearCompleted_AppearsWithCompletedTodos_AndRemovesThem()
    {
        var (wrapper, store) = MountWithTodos("a", "b");
        using (wrapper)
        {
            wrapper.Find(".clear-completed").ShouldBeNull();

            await wrapper.FindAll(".toggle")[0].Trigger("change");
            wrapper.Find(".clear-completed").ShouldNotBeNull();

            await wrapper.Get(".clear-completed").Trigger("click");
            store.Items.Count.ShouldBe(1);
            wrapper.Find(".clear-completed").ShouldBeNull();
        }
    }

    [Fact]
    public async Task DoubleClickingALabel_EntersEditMode_AndBlurCommitsAndExits()
    {
        var (wrapper, _) = MountWithTodos("a", "b");
        using (wrapper)
        {
            await wrapper.FindAll(".todo-item")[0].Get("label").Trigger("dblclick");

            wrapper.FindAll(".is-editing").Count.ShouldBe(1);
            wrapper.Find(".edit").ShouldNotBeNull();

            await wrapper.Get(".edit").Trigger("blur");
            wrapper.FindAll(".is-editing").Count.ShouldBe(0);
        }
    }
}
