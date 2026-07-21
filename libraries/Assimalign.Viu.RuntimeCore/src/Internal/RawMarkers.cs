using System.Runtime.CompilerServices;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// The identity-keyed set of objects excluded from reactivity by <see cref="Reactive.MarkRaw{T}"/> —
/// the C# port of Vue 3.5's <c>markRaw()</c> <c>SKIP</c> flag
/// (https://vuejs.org/api/reactivity-advanced.html#markraw). Viu has no proxy wrapper, so instead of
/// stamping a hidden property on a target it records the instance in a
/// <see cref="ConditionalWeakTable{TKey,TValue}"/>: membership is by reference identity, entries are
/// held weakly so marking never keeps an object alive, and there is no reflection (trim/AOT-safe).
/// Consulted by <see cref="ReactiveTraversal.Visit"/> (deep watch skips marked objects) and
/// <see cref="Reactive.IsReactive"/> (a marked object reports as non-reactive). Not thread-safe
/// (single-threaded JS event-loop model).
/// </summary>
internal static class RawMarkers
{
    private static readonly ConditionalWeakTable<object, object> Marked = new();

    // A single shared value: only key presence matters, never the stored value.
    private static readonly object Present = new();

    /// <summary>Marks <paramref name="value"/> as raw (idempotent).</summary>
    /// <param name="value">The non-null instance to exclude from reactivity.</param>
    internal static void Mark(object value) => Marked.AddOrUpdate(value, Present);

    /// <summary>Whether <paramref name="value"/> has been marked raw.</summary>
    /// <param name="value">The instance to test.</param>
    /// <returns><see langword="true"/> when marked.</returns>
    internal static bool IsMarked(object value) => Marked.TryGetValue(value, out _);
}
