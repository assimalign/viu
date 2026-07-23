using System;

namespace Assimalign.Viu;

/// <summary>
/// A typed, identity-based provide/inject key — the C# port of upstream's
/// <c>InjectionKey&lt;T&gt;</c> (<c>packages/runtime-core/src/apiInject.ts</c>,
/// https://vuejs.org/api/composition-api-dependency-injection.html#injectionkey). Upstream keys
/// are ES <c>Symbol</c>s carrying a phantom value type; C# has no <c>Symbol</c>, so this stands in
/// with <b>reference identity</b> (two distinct instances are distinct keys, even with the same
/// <see cref="Name"/>) and a phantom <typeparamref name="T"/> that lets
/// <see cref="DependencyInjection.Provide{T}(InjectionKey{T}, T)"/> and
/// <see cref="DependencyInjection.Inject{T}(InjectionKey{T})"/> round-trip strongly typed with no
/// cast at the call site. Declare keys as <c>static readonly</c> singletons and share them between
/// the provider and the injector.
/// <para>
/// Deliberately a <c>class</c>, not a <c>record</c>: a record's value equality would make every
/// same-typed key collide, breaking the <c>Symbol</c> identity contract. Trimming-safe — the type
/// argument is phantom and never activated reflectively.
/// </para>
/// </summary>
/// <typeparam name="T">The type of the value provided under this key.</typeparam>
public sealed class InjectionKey<T>
{
    /// <summary>Creates an anonymous key whose identity is the instance itself.</summary>
    public InjectionKey()
    {
    }

    /// <summary>Creates a key with a diagnostic <paramref name="name"/> (identity is still the instance).</summary>
    /// <param name="name">A description used in warnings and <see cref="ToString"/>; not part of key identity.</param>
    public InjectionKey(string? name)
    {
        Name = name;
    }

    /// <summary>The diagnostic description, or null. Does not participate in key identity.</summary>
    public string? Name { get; }

    /// <summary>Returns <see cref="Name"/> when set, otherwise a type-qualified placeholder.</summary>
    public override string ToString() => Name ?? $"InjectionKey<{typeof(T).Name}>";
}
