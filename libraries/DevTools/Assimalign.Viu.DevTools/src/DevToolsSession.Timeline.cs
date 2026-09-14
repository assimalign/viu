using System;
using System.Collections.Generic;
using System.Globalization;

using Assimalign.Viu;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.DevTools;

public sealed partial class DevToolsSession : IReactivityInspectionHook, IRuntimeSchedulerInspectionHook
{
    private readonly TimelineRecorder _timeline;
    private readonly Stack<int> _activeEffects = new();
    private IDisposable? _reactivityHookRegistration;
    private TimelineEventPayload _pendingFlushStart;
    private bool _insideInspectedFlush;
    private bool _flushStartRecorded;
    private bool _pendingApplicationTimeline;
    private int _inspectionDrainCount;

    private bool CanRecordTimeline => !_disposed && _negotiatedVersion.HasValue;

    private static DevToolsTimelineLayer[] CreateBuiltInTimelineLayers() =>
    [
        new("reactivity", "Reactivity", "#8b5cf6"),
        new("components", "Components", "#22c55e"),
        new("scheduler", "Scheduler", "#f59e0b"),
    ];

    void IReactivityInspectionHook.DependencyTracked(in ReactivityInspectionDependency dependency) =>
        RecordDependency(in dependency, triggered: false);

    void IReactivityInspectionHook.DependencyTriggered(in ReactivityInspectionDependency dependency) =>
        RecordDependency(in dependency, triggered: true);

    private void RecordDependency(in ReactivityInspectionDependency dependency, bool triggered)
    {
        lock (_messagesSynchronization)
        {
            if (!CanRecordTimeline)
            {
                return;
            }

            EnsureFlushStart();
            int identifier = _timeline.GetIdentifier(dependency.Dependency);
            string label = _timeline.GetLabel(
                dependency.Dependency, dependency.MemberKey, dependency.DebugLabel);
            int? subscriberIdentifier = dependency.Subscriber is null
                ? null : _timeline.GetIdentifier(dependency.Subscriber);
            int? ownerIdentifier = dependency.Owner is null
                ? null : _timeline.GetIdentifier(dependency.Owner);
            if (triggered && dependency.IsWrite)
            {
                TimelineEventPayload write = CreateTimelineEvent("reactivity", "state.write", label) with
                {
                    DependencyIdentifier = identifier,
                    EffectIdentifier = subscriberIdentifier,
                    OwnerIdentifier = ownerIdentifier,
                    Version = dependency.Version,
                };
                _timeline.Record(in write);
            }

            TimelineEventPayload observation = CreateTimelineEvent(
                "reactivity", triggered ? "dependency.triggered" : "dependency.tracked", label) with
            {
                DependencyIdentifier = identifier,
                EffectIdentifier = subscriberIdentifier,
                OwnerIdentifier = ownerIdentifier,
                Version = dependency.Version,
            };
            _timeline.Record(in observation);
            _pendingApplicationTimeline = true;
        }

        QueueFlush();
    }

    void IReactivityInspectionHook.EffectScheduled(in ReactivityInspectionEffect effect) =>
        RecordEffect(in effect, "effect.scheduled");

    void IReactivityInspectionHook.EffectRunStarted(in ReactivityInspectionEffect effect) =>
        RecordEffect(in effect, "effect.run.started");

    void IReactivityInspectionHook.EffectRunCompleted(in ReactivityInspectionEffect effect) =>
        RecordEffect(in effect, "effect.run.completed");

    private void RecordEffect(in ReactivityInspectionEffect effect, string kind)
    {
        lock (_messagesSynchronization)
        {
            if (!CanRecordTimeline)
            {
                return;
            }

            EnsureFlushStart();
            int identifier = _timeline.GetIdentifier(effect.Effect);
            if (kind == "effect.run.started")
            {
                _activeEffects.Push(identifier);
            }

            TimelineEventPayload observation = CreateTimelineEvent(
                "reactivity", kind, "effect-" + identifier.ToString(CultureInfo.InvariantCulture)) with
            {
                EffectIdentifier = identifier,
                DependencyIdentifier = effect.Cause is null ? null : _timeline.GetIdentifier(effect.Cause),
                Succeeded = kind == "effect.run.completed" ? effect.Succeeded : null,
            };
            _timeline.Record(in observation);
            if (kind == "effect.run.completed" && _activeEffects.TryPeek(out int current)
                && current == identifier)
            {
                _activeEffects.Pop();
            }

            _pendingApplicationTimeline = true;
        }

        QueueFlush();
    }

    private void RecordComponent(string kind, int identifier, string label)
    {
        lock (_messagesSynchronization)
        {
            if (!CanRecordTimeline)
            {
                return;
            }

            EnsureFlushStart();
            TimelineEventPayload observation = CreateTimelineEvent("components", kind, label) with
            {
                ComponentIdentifier = identifier,
                EffectIdentifier = _activeEffects.TryPeek(out int effect) ? effect : null,
            };
            _timeline.Record(in observation);
            _pendingApplicationTimeline = true;
        }

        QueueFlush();
    }

    void IRuntimeSchedulerInspectionHook.FlushStarted(in RuntimeInspectionFlush flush)
    {
        lock (_messagesSynchronization)
        {
            _insideInspectedFlush = true;
            _inspectionDrainCount = 0;
            _flushStartRecorded = false;
            _pendingFlushStart = CreateTimelineEvent("scheduler", "flush.started", "scheduler flush") with
            {
                CorrelationIdentifier = flush.Identifier,
            };
            if (_pendingApplicationTimeline && CanRecordTimeline)
            {
                EnsureFlushStart();
            }
        }
    }

    void IRuntimeSchedulerInspectionHook.FlushCompleted(in RuntimeInspectionFlush flush)
    {
        lock (_messagesSynchronization)
        {
            bool applicationWork = _pendingApplicationTimeline
                || flush.PreFlushCount > 0 || flush.RenderCount > 0
                || flush.PostFlushCount > _inspectionDrainCount;
            if (CanRecordTimeline && applicationWork)
            {
                EnsureFlushStart();
                TimelineEventPayload completed = CreateTimelineEvent(
                    "scheduler", "flush.completed", "scheduler flush") with
                {
                    CorrelationIdentifier = flush.Identifier,
                    PreFlushCount = flush.PreFlushCount,
                    RenderCount = flush.RenderCount,
                    PostFlushCount = Math.Max(0, flush.PostFlushCount - _inspectionDrainCount),
                    Succeeded = !flush.Faulted,
                };
                _timeline.Record(in completed);
            }

            _insideInspectedFlush = false;
            _pendingApplicationTimeline = false;
        }

        // Scheduling another scheduler job here would inspect the recorder forever. This only
        // starts an asynchronous drain; serialization and I/O begin after the hook has returned.
        Flush();
    }

    private void EnsureFlushStart()
    {
        if (_insideInspectedFlush && !_flushStartRecorded)
        {
            _timeline.Record(in _pendingFlushStart);
            _flushStartRecorded = true;
        }
    }

    private TimelineEventPayload CreateTimelineEvent(string layer, string kind, string label) => new(
        _nextMessageSequence++,
        _timeline.GetTimestamp(),
        RuntimeInspection.FlushIdentifier,
        layer,
        kind,
        label);
}
