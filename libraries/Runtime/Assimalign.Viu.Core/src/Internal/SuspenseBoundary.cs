using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Assimalign.Viu;

/// <summary>Owns one single-threaded pending generation and its reveal effects [BLT-13].</summary>
internal sealed class SuspenseBoundary : IDisposable
{
    private readonly Dictionary<Task, int> _dependencies = [];
    private readonly List<SchedulerJob> _effects = [];
    private readonly TaskCompletionSource _completion = new();
    private bool _hasDependencies;
    private bool _parentRegistered;

    internal SuspenseBoundary(SuspenseBoundary? parent = null) => Parent = parent;

    internal event Action? PendingStarted;
    internal event Action? Resolved;
    internal event Action? Failed;

    internal SuspenseBoundary? Parent { get; }
    internal int PendingCount { get; private set; }
    internal bool IsPending { get; private set; } = true;
    internal bool IsFailed { get; private set; }
    internal bool IsDisposed { get; private set; }
    internal bool IsRetired { get; private set; }

    internal bool IsHidden => IsPending || Parent is { IsHidden: true };

    internal void Register(Task dependency, RuntimeComponentContext? owner = null)
    {
        ArgumentNullException.ThrowIfNull(dependency);
        if (!IsPending || IsDisposed || IsFailed || IsRetired)
        {
            return;
        }

        _dependencies.TryGetValue(dependency, out int count);
        _dependencies[dependency] = count + 1;
        PendingCount++;
        if (!_hasDependencies)
        {
            _hasDependencies = true;
            if (Parent is { IsPending: true, IsDisposed: false, IsFailed: false })
            {
                _parentRegistered = true;
                Parent.Register(_completion.Task, owner);
            }

            PendingStarted?.Invoke();
        }
    }

    internal void Settle(Task dependency)
    {
        ArgumentNullException.ThrowIfNull(dependency);
        if (IsDisposed || !_dependencies.TryGetValue(dependency, out int count))
        {
            return;
        }

        if (count == 1)
        {
            _dependencies.Remove(dependency);
        }
        else
        {
            _dependencies[dependency] = count - 1;
        }

        if (--PendingCount == 0 && !IsFailed)
        {
            Resolved?.Invoke();
        }
    }

    internal void QueueEffect(SchedulerJob effect)
    {
        if (IsDisposed || IsFailed || effect.IsDisposed)
        {
            return;
        }

        if (IsPending)
        {
            if (!_effects.Contains(effect))
            {
                _effects.Add(effect);
            }
        }
        else if (Parent is not null)
        {
            Parent.QueueEffect(effect);
        }
        else
        {
            Scheduler.QueuePostFlushCallback(effect);
        }
    }

    internal void Reveal()
    {
        if (IsDisposed || IsFailed || PendingCount != 0)
        {
            return;
        }

        IsPending = false;
        foreach (SchedulerJob effect in _effects)
        {
            if (Parent is not null)
            {
                Parent.QueueEffect(effect);
            }
            else
            {
                Scheduler.QueuePostFlushCallback(effect);
            }
        }

        _effects.Clear();
        ReleaseParent();
    }

    internal void Fail(Exception error, RuntimeComponentContext? owner = null)
    {
        if (IsDisposed || IsFailed)
        {
            return;
        }

        IsFailed = true;
        _effects.Clear();
        Failed?.Invoke();
        if (_parentRegistered)
        {
            Parent?.Fail(error, owner);
        }
    }

    private void ReleaseParent()
    {
        if (_parentRegistered)
        {
            _parentRegistered = false;
            Parent?.Settle(_completion.Task);
        }
    }

    internal void Retire()
    {
        IsRetired = true;
        PendingStarted = null;
        Resolved = null;
        Failed = null;
    }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        PendingStarted = null;
        Resolved = null;
        Failed = null;
        _dependencies.Clear();
        _effects.Clear();
        PendingCount = 0;
        ReleaseParent();
    }
}
