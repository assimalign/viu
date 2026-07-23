using System;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// The value-watching <see cref="Watcher"/> behind <c>Watch(source, callback)</c> — the C# port of
/// the getter/old-value/callback loop in Vue 3.5's <c>baseWatch</c>
/// (<c>packages/reactivity/src/watch.ts</c>). Each reaction re-runs the (possibly deep-traversing)
/// getter inside the effect to collect the new value and refresh dependencies, then delivers the
/// callback when the value changed — or unconditionally for a deep/forced watcher. The previous value
/// is preserved across reactions and handed to the callback as <c>oldValue</c>.
/// </summary>
/// <typeparam name="T">The watched value type (an object array for the multi-source form).</typeparam>
internal sealed class Watcher<T> : Watcher
{
    private readonly Func<T> _getter;
    private readonly WatchCallback<T> _callback;
    private readonly Func<T, T, bool> _hasChanged;
    private readonly bool _alwaysCallback;
    private T _oldValue = default!;
    private T _pendingValue = default!;

    internal Watcher(
        Func<T> getter,
        WatchCallback<T> callback,
        Func<T, T, bool> hasChanged,
        bool alwaysCallback,
        T unsetOldValue,
        bool immediate,
        WatchFlushMode flush,
        IReactiveWatchScheduler? scheduler,
        bool once)
        : base(flush, scheduler, once)
    {
        _getter = getter;
        _callback = callback;
        _hasChanged = hasChanged;
        _alwaysCallback = alwaysCallback;

        Effect = new ReactiveEffect(RunGetter);
        Initialize();
        try
        {
            // Initial tracked run: collect dependencies and the baseline value (upstream runs the
            // getter immediately so oldValue is populated for the first real change).
            Effect.Run();
            var initial = _pendingValue;
            _oldValue = initial;
            if (immediate)
            {
                // Immediate first callback: oldValue is unset (default / empty array).
                _callback(initial, unsetOldValue, RegisterCleanup);
                AfterCallback();
            }
        }
        catch
        {
            // A throwing initial run leaves no live subscription (Reactive.Effect parity).
            Stop();
            throw;
        }
    }

    /// <inheritdoc />
    protected override void React()
    {
        Effect.Run();
        var newValue = _pendingValue;
        if (_alwaysCallback || _hasChanged(newValue, _oldValue))
        {
            RunCleanup();
            var oldValue = _oldValue;
            // Advance the baseline before invoking the callback so a synchronous re-trigger from
            // within the callback observes the updated old value.
            _oldValue = newValue;
            _callback(newValue, oldValue, RegisterCleanup);
            AfterCallback();
        }
        else
        {
            _oldValue = newValue;
        }
    }

    private void RunGetter() => _pendingValue = _getter();
}
