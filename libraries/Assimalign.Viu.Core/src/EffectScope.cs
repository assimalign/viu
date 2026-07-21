using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace Assimalign.Viu;

/// <summary>
/// Collects every effect and cleanup callback created while it is the ambient current scope, and
/// disposes them in bulk — the C# port of Vue's <c>effectScope()</c>. Computeds are never owned
/// by a scope (upstream Vue 3.5 parity): a computed created inside a scope keeps serving fresh
/// values after <see cref="Stop()"/>; its cleanup is automatic, driven by the subscriber count
/// (losing the last subscriber soft-detaches it from its sources). Nested scopes register with
/// (and stop with) their parent unless created detached. The ambient <see cref="Current"/> scope
/// is a plain static field: NOT thread-safe by design, per the single-threaded JS event-loop
/// model.
/// </summary>
public sealed class EffectScope : IDisposable
{
    private static EffectScope? _current;

    private readonly bool _detached;
    private readonly List<ReactiveEffect> _effects = new();
    private readonly List<Action> _cleanups = new();
    private List<EffectScope>? _scopes;
    private EffectScope? _parent;
    private int _index;
    private bool _active = true;
    private bool _paused;

    /// <summary>
    /// Creates a scope. Unless <paramref name="detached"/> is <see langword="true"/>, the scope
    /// registers as a child of the current scope and will be stopped with it.
    /// </summary>
    /// <param name="detached">When true, the scope does not attach to the current scope.</param>
    public EffectScope(bool detached = false)
    {
        _detached = detached;
        _parent = _current;
        if (!detached && _current is not null)
        {
            _index = (_current._scopes ??= new List<EffectScope>()).Count;
            _current._scopes.Add(this);
        }
    }

    /// <summary>The ambient scope that new effects and computeds register with, if any.</summary>
    public static EffectScope? Current => _current;

    /// <summary>Whether the scope has not been stopped.</summary>
    public bool IsActive => _active;

    /// <summary>
    /// Runs <paramref name="action"/> with this scope as the current scope, restoring the previous
    /// scope afterwards (even on throw). A stopped scope executes the action without becoming
    /// current — it no longer collects anything.
    /// </summary>
    /// <param name="action">The code to run inside the scope.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    public void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var previous = _current;
        if (_active)
        {
            _current = this;
        }
        try
        {
            action();
        }
        finally
        {
            _current = previous;
        }
    }

    /// <summary>
    /// Runs <paramref name="function"/> with this scope as the current scope and returns its result,
    /// restoring the previous scope afterwards (even on throw).
    /// </summary>
    /// <typeparam name="TResult">The function's return type.</typeparam>
    /// <param name="function">The code to run inside the scope.</param>
    /// <returns>The value returned by <paramref name="function"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="function"/> is null.</exception>
    public TResult Run<TResult>(Func<TResult> function)
    {
        ArgumentNullException.ThrowIfNull(function);
        var previous = _current;
        if (_active)
        {
            _current = this;
        }
        try
        {
            return function();
        }
        finally
        {
            _current = previous;
        }
    }

    /// <summary>Pauses all contained effects and child scopes (Vue 3.5 parity).</summary>
    public void Pause()
    {
        if (!_active || _paused)
        {
            return;
        }
        _paused = true;
        if (_scopes is not null)
        {
            foreach (var scope in _scopes)
            {
                scope.Pause();
            }
        }
        foreach (var effect in _effects)
        {
            effect.Pause();
        }
    }

    /// <summary>Resumes all contained effects and child scopes; pending triggers deliver one trailing run each.</summary>
    public void Resume()
    {
        if (!_active || !_paused)
        {
            return;
        }
        _paused = false;
        if (_scopes is not null)
        {
            foreach (var scope in _scopes)
            {
                scope.Resume();
            }
        }
        foreach (var effect in _effects)
        {
            effect.Resume();
        }
    }

    /// <summary>
    /// Stops every collected effect, runs cleanup callbacks in registration order, stops child
    /// scopes, and detaches from the parent. Computeds are unaffected — they are never owned by a
    /// scope (upstream Vue 3.5 parity). Exception-safe: a throwing
    /// <see cref="ReactiveEffect.OnStop"/>, cleanup callback, or child scope does not abandon the
    /// remaining teardown; the first exception is rethrown after everything has been stopped.
    /// Idempotent.
    /// </summary>
    public void Stop() => Stop(fromParent: false);

    /// <summary>Stops the scope; equivalent to <see cref="Stop()"/> for <c>using</c> support.</summary>
    public void Dispose() => Stop();

    internal void RegisterEffect(ReactiveEffect effect)
    {
        if (_active)
        {
            _effects.Add(effect);
        }
    }

    internal void RegisterCleanup(Action cleanup) => _cleanups.Add(cleanup);

    private void Stop(bool fromParent)
    {
        if (!_active)
        {
            return;
        }
        _active = false;

        // Exception safety (mirrors the EndBatch pattern): capture the FIRST exception thrown by
        // user code, keep tearing everything else down, and rethrow at the end — one throwing
        // callback must not leave live effects or unstopped child scopes behind.
        ExceptionDispatchInfo? error = null;
        foreach (var effect in _effects)
        {
            try
            {
                effect.Stop();
            }
            catch (Exception exception)
            {
                error ??= ExceptionDispatchInfo.Capture(exception);
            }
        }
        _effects.Clear();
        foreach (var cleanup in _cleanups)
        {
            try
            {
                cleanup();
            }
            catch (Exception exception)
            {
                error ??= ExceptionDispatchInfo.Capture(exception);
            }
        }
        _cleanups.Clear();
        if (_scopes is not null)
        {
            // Detach the child list before iterating: a re-entrant Stop() on a child (e.g. from a
            // sibling's cleanup) then finds no parent list to swap-remove from, so it cannot
            // invalidate this enumeration; already-stopped children are harmless no-ops.
            var scopes = _scopes;
            _scopes = null;
            foreach (var scope in scopes)
            {
                try
                {
                    scope.Stop(fromParent: true);
                }
                catch (Exception exception)
                {
                    error ??= ExceptionDispatchInfo.Capture(exception);
                }
            }
        }
        // O(1) swap-removal from the parent's child list (Vue parity).
        if (!_detached && !fromParent && _parent?._scopes is { Count: > 0 } siblings)
        {
            var last = siblings[^1];
            siblings.RemoveAt(siblings.Count - 1);
            if (!ReferenceEquals(last, this))
            {
                siblings[_index] = last;
                last._index = _index;
            }
        }
        _parent = null;
        error?.Throw();
    }
}
