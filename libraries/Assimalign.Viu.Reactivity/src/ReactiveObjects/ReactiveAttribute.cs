using System;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// Marks a <see langword="partial"/> class as deeply reactive. Reactivity is emitted at build
/// time rather than intercepted at runtime, which is what keeps the model trimming- and AOT-safe
/// (<c>[RCT-6]</c>). The
/// <c>Assimalign.Viu.Generators.Reactivity</c> source generator fills in every <c>partial</c>
/// property with a per-property <see cref="Dependency"/>: the getter tracks, the setter triggers
/// only on an <see cref="System.Collections.Generic.EqualityComparer{T}"/> change. The generated
/// type implements <see cref="IReactiveObject"/>, so nested <c>[Reactive]</c> objects, references, and
/// reactive collections compose and are reachable by a deep <c>watch</c> without reflection.
/// <para>
/// Consequences of the build-time model: the reactive member set is fixed at compile time (no
/// dynamic property addition),
/// and the annotated instance is itself reactive — there is no identity-swapping proxy wrapper. For
/// root-level-only tracking use <see cref="ShallowReactiveAttribute"/>.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ReactiveAttribute : Attribute
{
    /// <summary>
    /// When <see langword="true"/>, generated setters reject writes with a dev-mode warning and do
    /// not mutate or trigger, while reads still track. Default <see langword="false"/>.
    /// </summary>
    public bool ReadOnly { get; set; }
}
