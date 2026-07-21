namespace Assimalign.Viu;

/// <summary>
/// Reports whether a reactive value is a read-only view — the C# port of Vue 3.5's
/// <c>ReactiveFlags.IS_READONLY</c> flag consulted by <c>isReadonly()</c>
/// (https://vuejs.org/api/reactivity-utilities.html#isreadonly). It is implemented by the two
/// read-only shapes in Viu: a getter-only <see cref="Computed{T}"/> (a computed with no setter)
/// and a source-generated <c>[Reactive(Readonly = true)]</c>/<c>[ShallowReactive(Readonly = true)]</c>
/// object (the port of <c>readonly()</c>/<c>shallowReadonly()</c>). Writable computeds and mutable
/// reactive objects do not implement it — or implement it returning <see langword="false"/> — so
/// <see cref="Reactive.IsReadonly"/> stays an O(1), reflection-free interface check.
/// </summary>
public interface IReadOnlyReactive
{
    /// <summary>
    /// <see langword="true"/> when this value rejects writes (reads still track). A getter-only
    /// computed reports <see langword="true"/>; a writable computed reports <see langword="false"/>.
    /// </summary>
    bool IsReadOnly { get; }
}
