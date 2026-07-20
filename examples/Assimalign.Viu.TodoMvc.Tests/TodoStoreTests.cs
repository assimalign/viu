using Shouldly;
using Xunit;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.TodoMvc.Tests;

// Pins the reactive TodoMVC store: trimming/blank rules, keyed ids, the mutation actions, and — the
// part where dependency-tracking bugs hide — the *run counts* of the computed views, so a computed
// that recomputes too often (or not often enough) fails the suite rather than passing on a
// coincidentally-correct value. TodoMVC spec: https://github.com/tastejs/todomvc/blob/master/app-spec.md
public sealed class TodoStoreTests
{
    [Fact]
    public void Add_TrimsTitle_AndIgnoresBlankInput()
    {
        var store = new TodoStore();

        var added = store.Add("  Buy milk  ");

        added.ShouldNotBeNull();
        added!.Title.ShouldBe("Buy milk");
        store.Items.Count.ShouldBe(1);

        store.Add("   ").ShouldBeNull();
        store.Add(null).ShouldBeNull();
        store.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void Add_AssignsUniqueIncrementingIds()
    {
        var store = new TodoStore();

        var first = store.Add("a");
        var second = store.Add("b");

        first!.Id.ShouldBe(1);
        second!.Id.ShouldBe(2);
    }

    [Fact]
    public void RemainingCount_RecomputesOnToggle_AndCoalescesEqualWrites()
    {
        var store = new TodoStore();
        store.Add("a");
        store.Add("b");

        var runs = 0;
        var remaining = -1;
        var effect = Reactive.Effect(() =>
        {
            runs++;
            remaining = store.RemainingCount.Value;
        });

        runs.ShouldBe(1);
        remaining.ShouldBe(2);

        store.Toggle(store.Items[0]);
        runs.ShouldBe(2);
        remaining.ShouldBe(1);

        // An equal-value write does not trigger (Vue's ref/reactive set only notifies on change), so
        // the computed does not recompute and the effect does not re-run.
        store.Items[0].Completed = true;
        runs.ShouldBe(2);
        remaining.ShouldBe(1);

        effect.Stop();
    }

    [Fact]
    public void Visible_FiltersByActiveAndCompleted()
    {
        var store = new TodoStore();
        var a = store.Add("a")!;
        var b = store.Add("b")!;
        store.Toggle(b);

        store.SetFilter(TodoFilter.All);
        store.Visible.Value.ShouldBe(new[] { a, b });

        store.SetFilter(TodoFilter.Active);
        store.Visible.Value.ShouldBe(new[] { a });

        store.SetFilter(TodoFilter.Completed);
        store.Visible.Value.ShouldBe(new[] { b });
    }

    [Fact]
    public void Visible_IsCached_AndRecomputesOnlyWhenADependencyChanges()
    {
        var store = new TodoStore();
        store.Add("a");
        store.Add("b");

        var runs = 0;
        var effect = Reactive.Effect(() =>
        {
            runs++;
            _ = store.Visible.Value;
        });
        runs.ShouldBe(1);

        // Re-selecting the current filter is an equal write: no trigger, no recompute.
        store.SetFilter(TodoFilter.All);
        runs.ShouldBe(1);

        // A real filter change recomputes exactly once.
        store.SetFilter(TodoFilter.Active);
        runs.ShouldBe(2);

        // Adding a todo changes the list (iteration dependency) and recomputes once.
        store.Add("c");
        runs.ShouldBe(3);

        effect.Stop();
    }

    [Fact]
    public void AllCompleted_IsTrueOnlyWhenNonEmptyAndEveryTodoComplete()
    {
        var store = new TodoStore();
        store.AllCompleted.Value.ShouldBeFalse();

        var a = store.Add("a")!;
        var b = store.Add("b")!;
        store.AllCompleted.Value.ShouldBeFalse();

        store.SetAll(true);
        store.AllCompleted.Value.ShouldBeTrue();
        a.Completed.ShouldBeTrue();
        b.Completed.ShouldBeTrue();

        store.SetAll(false);
        store.AllCompleted.Value.ShouldBeFalse();
    }

    [Fact]
    public void ClearCompleted_RemovesOnlyCompletedTodos()
    {
        var store = new TodoStore();
        var a = store.Add("a")!;
        var b = store.Add("b")!;
        var c = store.Add("c")!;
        store.Toggle(a);
        store.Toggle(c);

        store.ClearCompleted();

        store.Items.Count.ShouldBe(1);
        store.Items[0].ShouldBe(b);
    }

    [Fact]
    public void Rename_SetsTrimmedTitle_AndAnEmptyEditRemovesTheTodo()
    {
        var store = new TodoStore();
        var a = store.Add("a")!;
        var b = store.Add("b")!;

        store.Rename(a, "  renamed  ");
        a.Title.ShouldBe("renamed");
        store.Items.Count.ShouldBe(2);

        store.Rename(b, "   ");
        store.Items.ShouldNotContain(b);
        store.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void CompletedCount_TracksToggles()
    {
        var store = new TodoStore();
        var a = store.Add("a")!;
        store.Add("b");

        store.CompletedCount.Value.ShouldBe(0);
        store.Toggle(a);
        store.CompletedCount.Value.ShouldBe(1);
    }
}
