using System;
using System.Collections.Generic;

namespace Assimalign.Vue.RuntimeCore;

/// <summary>
/// Composition API dependency injection — the C# port of <c>provide()</c> and <c>inject()</c>
/// from <c>packages/runtime-core/src/apiInject.ts</c>
/// (https://vuejs.org/guide/components/provide-inject.html). A value provided by an ancestor is
/// injectable by any descendant regardless of depth; a nearer provider shadows a farther one.
/// Both APIs bind to <see cref="ComponentInstance.Current"/> and must be called during
/// <c>Setup</c>; calling them with no active instance produces the upstream dev warning and is
/// otherwise inert.
/// <para>
/// <b>Provides representation.</b> Upstream gives each instance a provides object whose prototype
/// is the parent's, so <c>inject</c> reads are O(1) via the prototype chain. C# has no prototype
/// chain, so this uses <b>copy-on-first-provide</b>: an instance inherits its parent's provides
/// table <i>by reference</i> until it provides a value of its own, at which point it forks a flat
/// dictionary seeded with a copy of the parent's entries (see
/// <see cref="ComponentInstance.Provides"/>). The trade-off: a provide costs O(n) in the ancestor
/// provide-count on first fork, but every <c>inject</c> is an O(1) dictionary probe with no
/// allocation on the hit path — the right bias, since injects vastly outnumber provides and run on
/// the render hot path. Because <c>Setup</c> runs parent-before-child, an ancestor's table is
/// always complete before a descendant copies it, so the flat copy stays correct.
/// </para>
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Provides <paramref name="value"/> under the typed <paramref name="key"/> for all
    /// descendants (upstream: <c>provide</c>). Overwrites any value this instance already provided
    /// under the same key.
    /// </summary>
    /// <typeparam name="T">The provided value type.</typeparam>
    /// <param name="key">The identity-based key descendants inject with.</param>
    /// <param name="value">The value to provide.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public static void Provide<T>(InjectionKey<T> key, T value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ProvideCore(key, value);
    }

    /// <summary>
    /// Provides <paramref name="value"/> under a string <paramref name="key"/> for all descendants
    /// (upstream: <c>provide</c> with a string key). String keys are matched by value; prefer
    /// <see cref="InjectionKey{T}"/> for type safety and collision resistance.
    /// </summary>
    /// <param name="key">The string key descendants inject with.</param>
    /// <param name="value">The value to provide.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null or empty.</exception>
    public static void Provide(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ProvideCore(key, value);
    }

    /// <summary>
    /// Injects the value provided under the typed <paramref name="key"/> by an ancestor (upstream:
    /// <c>inject</c>). Missing key with no default: a dev warning fires and <c>default</c> is
    /// returned.
    /// </summary>
    /// <typeparam name="T">The injected value type.</typeparam>
    /// <param name="key">The key an ancestor provided under.</param>
    /// <returns>The provided value, or <c>default</c> when absent.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public static T? Inject<T>(InjectionKey<T> key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Lookup(key, out var value) switch
        {
            InjectionLookup.Found => (T?)value,
            InjectionLookup.NoInstance => WarnOutsideSetup<T?>(default),
            _ => WarnNotFound<T?>(key, default),
        };
    }

    /// <summary>
    /// Injects the value provided under the typed <paramref name="key"/>, falling back to
    /// <paramref name="defaultValue"/> when no ancestor provided it (upstream: <c>inject</c> with a
    /// default value). No "not found" warning fires when a default is supplied.
    /// </summary>
    /// <typeparam name="T">The injected value type.</typeparam>
    /// <param name="key">The key an ancestor provided under.</param>
    /// <param name="defaultValue">The value returned when the key is absent.</param>
    /// <returns>The provided value, or <paramref name="defaultValue"/> when absent.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public static T Inject<T>(InjectionKey<T> key, T defaultValue)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Lookup(key, out var value) switch
        {
            InjectionLookup.Found => (T)value!,
            InjectionLookup.NoInstance => WarnOutsideSetup(defaultValue),
            _ => defaultValue,
        };
    }

    /// <summary>
    /// Injects the value provided under the typed <paramref name="key"/>, invoking
    /// <paramref name="defaultFactory"/> for the fallback when no ancestor provided it (upstream:
    /// <c>inject</c> with <c>treatDefaultAsFactory: true</c> — choosing the factory overload is the
    /// idiomatic C# form of that flag). The factory runs only on a miss.
    /// </summary>
    /// <typeparam name="T">The injected value type.</typeparam>
    /// <param name="key">The key an ancestor provided under.</param>
    /// <param name="defaultFactory">Produces the fallback when the key is absent.</param>
    /// <returns>The provided value, or <paramref name="defaultFactory"/>'s result when absent.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="defaultFactory"/> is null.</exception>
    public static T Inject<T>(InjectionKey<T> key, Func<T> defaultFactory)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(defaultFactory);
        return Lookup(key, out var value) switch
        {
            InjectionLookup.Found => (T)value!,
            InjectionLookup.NoInstance => WarnOutsideSetup(defaultFactory()),
            _ => defaultFactory(),
        };
    }

    /// <summary>
    /// Injects the value provided under a string <paramref name="key"/> (upstream: <c>inject</c>
    /// with a string key). Missing key with no default: a dev warning fires and null is returned.
    /// </summary>
    /// <param name="key">The string key an ancestor provided under.</param>
    /// <returns>The provided value, or null when absent.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null or empty.</exception>
    public static object? Inject(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        return Lookup(key, out var value) switch
        {
            InjectionLookup.Found => value,
            InjectionLookup.NoInstance => WarnOutsideSetup<object?>(null),
            _ => WarnNotFound<object?>(key, null),
        };
    }

    /// <summary>
    /// Injects the value provided under a string <paramref name="key"/>, falling back to
    /// <paramref name="defaultValue"/> when absent (upstream: <c>inject(key, default,
    /// treatDefaultAsFactory)</c>). When <paramref name="treatDefaultAsFactory"/> is true and
    /// <paramref name="defaultValue"/> is a parameterless delegate, the delegate is invoked for the
    /// fallback; otherwise <paramref name="defaultValue"/> is returned as-is.
    /// </summary>
    /// <param name="key">The string key an ancestor provided under.</param>
    /// <param name="defaultValue">The fallback value, or a factory when <paramref name="treatDefaultAsFactory"/> is true.</param>
    /// <param name="treatDefaultAsFactory">Whether a delegate <paramref name="defaultValue"/> is invoked to produce the fallback.</param>
    /// <returns>The provided value, or the resolved default when absent.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null or empty.</exception>
    public static object? Inject(string key, object? defaultValue, bool treatDefaultAsFactory = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        return Lookup(key, out var value) switch
        {
            InjectionLookup.Found => value,
            InjectionLookup.NoInstance => WarnOutsideSetup(ResolveDefault(defaultValue, treatDefaultAsFactory)),
            _ => ResolveDefault(defaultValue, treatDefaultAsFactory),
        };
    }

    private static object? ResolveDefault(object? defaultValue, bool treatDefaultAsFactory)
        => treatDefaultAsFactory && defaultValue is Func<object?> factory ? factory() : defaultValue;

    private static void ProvideCore(object key, object? value)
    {
        var instance = ComponentInstance.Current;
        if (instance is null)
        {
            RuntimeWarnings.Warn("provide() can only be used inside Setup().");
            return;
        }
        var parentProvides = instance.Parent?.Provides;
        if (ReferenceEquals(instance.Provides, parentProvides))
        {
            // First own provide: fork a flat table layered over the inherited one (upstream:
            // provides = Object.create(parentProvides)). Setup runs parent-before-child, so the
            // parent's table is already complete and the copy is a correct snapshot.
            instance.Provides = parentProvides is null
                ? new Dictionary<object, object?>()
                : new Dictionary<object, object?>(parentProvides);
        }
        instance.Provides![key] = value;
    }

    private static InjectionLookup Lookup(object key, out object? value)
    {
        value = null;
        var instance = ComponentInstance.Current;
        if (instance is null)
        {
            return InjectionLookup.NoInstance;
        }
        // Upstream reads from the parent's provides, never the instance's own: a component that
        // both provides and injects the same key sees the ancestor value, not the one it just
        // provided for its descendants.
        var provides = instance.Parent?.Provides;
        if (provides is not null && provides.TryGetValue(key, out value))
        {
            return InjectionLookup.Found;
        }
        // App-level provides are the final fallback ([V01.01.03.12], issue #28). Upstream seeds the
        // root instance's provides prototype chain from appContext.provides, so an inject that
        // misses the component-ancestor chain (a nearer component provider always shadows here
        // because the flat table already holds every ancestor's entries) resolves against the app.
        var appProvides = instance.AppContext?.Provides;
        if (appProvides is not null && appProvides.TryGetValue(key, out value))
        {
            return InjectionLookup.Found;
        }
        return InjectionLookup.NotFound;
    }

    private static TValue WarnOutsideSetup<TValue>(TValue fallback)
    {
        RuntimeWarnings.Warn("inject() can only be used inside Setup().");
        return fallback;
    }

    private static TValue WarnNotFound<TValue>(object key, TValue fallback)
    {
        RuntimeWarnings.Warn($"injection \"{key}\" not found.");
        return fallback;
    }

    private enum InjectionLookup
    {
        Found,
        NotFound,
        NoInstance,
    }
}
