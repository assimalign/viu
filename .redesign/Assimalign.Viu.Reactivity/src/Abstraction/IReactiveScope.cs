using System;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// Owns reactive effects and cleanup callbacks as one disposable lifetime. The shipping
/// implementation is the abstraction boundary over the current <c>EffectScope</c>.
/// </summary>
public interface IReactiveScope : IDisposable
{
    /// <summary>Gets whether the scope can still collect reactive work.</summary>
    bool IsActive { get; }

    /// <summary>Runs an action with this scope as the ambient reactive scope.</summary>
    /// <param name="action">The action to run.</param>
    void Run(Action action);

    /// <summary>Runs a function with this scope as the ambient reactive scope.</summary>
    /// <typeparam name="TResult">The function result type.</typeparam>
    /// <param name="function">The function to run.</param>
    /// <returns>The function result.</returns>
    TResult Run<TResult>(Func<TResult> function);

    /// <summary>Stops owned effects, child scopes, and cleanup callbacks. Idempotent.</summary>
    void Stop();
}

