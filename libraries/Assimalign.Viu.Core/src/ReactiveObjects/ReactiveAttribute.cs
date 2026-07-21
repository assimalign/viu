using System;

namespace Assimalign.Viu;

/// <summary>
/// Marks a <see langword="partial"/> class as deeply reactive — the compiled C# counterpart of Vue
/// 3.5's <c>reactive()</c> (https://vuejs.org/api/reactivity-core.html#reactive). The
/// <c>Assimalign.Viu.Core.Generators</c> source generator fills in every <c>partial</c>
/// property with a per-property <see cref="Dependency"/>: the getter tracks, the setter triggers
/// only on an <see cref="System.Collections.Generic.EqualityComparer{T}"/> change. The generated
/// type implements <see cref="IReactiveObject"/>, so nested <c>[Reactive]</c> objects, refs, and
/// reactive collections compose and are reachable by a deep <c>watch</c> without reflection.
/// <para>
/// Parity notes: the reactive member set is fixed at compile time (no dynamic property addition),
/// and the annotated instance is itself reactive — there is no identity-swapping proxy wrapper. For
/// root-level-only tracking use <see cref="ShallowReactiveAttribute"/>.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ReactiveAttribute : Attribute
{
    /// <summary>
    /// When <see langword="true"/>, generated setters reject writes with a dev-mode warning and do
    /// not mutate or trigger, while reads still track — the port of Vue's <c>readonly()</c>
    /// (https://vuejs.org/api/reactivity-advanced.html#readonly). Default <see langword="false"/>.
    /// </summary>
    public bool Readonly { get; set; }
}
