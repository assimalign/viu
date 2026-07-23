using System;

namespace Assimalign.Viu;

/// <summary>
/// Marks a <see langword="partial"/> class as shallowly reactive — the compiled C# counterpart of
/// Vue 3.5's <c>shallowReactive()</c> (https://vuejs.org/api/reactivity-advanced.html#shallowreactive).
/// Like <see cref="ReactiveAttribute"/> the generator tracks and triggers each <c>partial</c>
/// property, but deep traversal stops at the root: replacing a property is reactive, whereas mutating
/// inside a nested value is not observed unless that nested value is itself a reactive primitive read
/// directly. Use <see cref="ReactiveAttribute"/> for deep-by-default composition.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ShallowReactiveAttribute : Attribute
{
    /// <summary>
    /// When <see langword="true"/>, generated setters reject writes with a dev-mode warning and do
    /// not mutate or trigger, while reads still track — the port of Vue's <c>shallowReadonly()</c>
    /// (https://vuejs.org/api/reactivity-advanced.html#shallowreadonly). Default <see langword="false"/>.
    /// </summary>
    public bool Readonly { get; set; }
}
