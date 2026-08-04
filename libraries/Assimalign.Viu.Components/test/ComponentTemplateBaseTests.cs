using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Components.Tests;

/// <summary>
/// Pins <c>[CMP-32]</c>: the protected registration methods on <see cref="ComponentTemplateBase"/> are
/// the specified equivalents of the <c>Context.Lifecycle</c> forms. The claim under test is
/// <b>interchangeability</b> — the same callback reaches the same registrar in the same position — plus
/// the collision story for a component that declares a member of its own with one of these names.
/// </summary>
public sealed class ComponentTemplateBaseTests
{
    // The registration surface [CMP-20] in the order the probe registers it, written out rather than
    // derived, so adding a hook to IComponentLifecycle without a root wrapper fails this test loudly.
    private static readonly string[] EveryHook =
    [
        "OnBeforeMount(Action)",
        "OnBeforeMount(Func`1)",
        "OnBeforeMount(Func`2)",
        "OnMounted(Action)",
        "OnMounted(Func`1)",
        "OnMounted(Func`2)",
        "OnBeforeUpdate(Action)",
        "OnBeforeUpdate(Func`1)",
        "OnBeforeUpdate(Func`2)",
        "OnUpdated(Action)",
        "OnUpdated(Func`1)",
        "OnUpdated(Func`2)",
        "OnBeforeUnmount(Action)",
        "OnBeforeUnmount(Func`1)",
        "OnBeforeUnmount(Func`2)",
        "OnUnmounted(Action)",
        "OnUnmounted(Func`1)",
        "OnUnmounted(Func`2)",
        "OnActivated(Action)",
        "OnActivated(Func`1)",
        "OnActivated(Func`2)",
        "OnDeactivated(Action)",
        "OnDeactivated(Func`1)",
        "OnDeactivated(Func`2)",
        "OnErrorCaptured(Func`4)",
        "OnServerPrefetch(Func`1)",
        "OnServerPrefetch(Func`2)",
    ];

    [Fact]
    public void EveryRootWrapper_RegistersWhatTheContextFormRegisters_InTheSameOrder()
    {
        // [CMP-32] The equivalence claim, exercised across the WHOLE surface at once: two probes register
        // the identical callback instances, one through the root forms and one through the context forms,
        // and the two registrars must have observed the same (hook, callback) sequence.
        LifecycleCallbacks callbacks = new();
        RecordingLifecycle throughRoot = new();
        RecordingLifecycle throughContext = new();

        new ProbeTemplate(new StubContext(throughRoot)).RegisterEveryRootForm(callbacks);
        new ProbeTemplate(new StubContext(throughContext)).RegisterEveryContextForm(callbacks);

        throughRoot.Registrations.Select(registration => registration.Hook).ShouldBe(EveryHook);
        throughRoot.Registrations.ShouldBe(throughContext.Registrations);
    }

    [Fact]
    public void RootSurface_CoversEveryLifecycleRegistrationMethod_AndIsProtected()
    {
        // [CMP-32] requires the COMPLETE set with identical signatures, so the surface is compared against
        // IComponentLifecycle itself rather than against a hand-written list. The accessibility assertion
        // pins "protected, not public": these are an authoring convenience inside the component, never
        // part of a component's public surface.
        var registrars = typeof(IComponentLifecycle)
            .GetMethods()
            .Where(method => !method.IsSpecialName) // the CancellationToken getter is not a registration
            .Select(Signature)
            .OrderBy(signature => signature, StringComparer.Ordinal)
            .ToList();
        var wrappers = typeof(ComponentTemplateBase)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName) // the Context property accessors
            .ToList();

        wrappers.ShouldAllBe(method => method.IsFamily);
        wrappers.Select(Signature)
            .OrderBy(signature => signature, StringComparer.Ordinal)
            .ShouldBe(registrars);
    }

    [Fact]
    public void MixedRootAndContextRegistrations_RunInRegistrationOrder_EachExactlyOnce()
    {
        // [CMP-32] Registration order is the order the registrations were MADE, regardless of which form
        // made each one — the property that lets a component mix the two freely. Run counts are asserted,
        // not just the sequence: a wrapper that registered twice would still produce a plausible order.
        RecordingLifecycle lifecycle = new();
        ProbeTemplate template = new(new StubContext(lifecycle));
        List<string> invocations = [];

        template.RegisterMountedAtRoot(() => invocations.Add("root-first"));
        template.RegisterMountedThroughContext(() => invocations.Add("context-second"));
        template.RegisterMountedAtRoot(() => invocations.Add("root-third"));
        template.RegisterMountedThroughContext(() => invocations.Add("context-fourth"));

        lifecycle.InvokeSynchronous("OnMounted(Action)");

        invocations.ShouldBe(["root-first", "context-second", "root-third", "context-fourth"]);
    }

    [Fact]
    public void ComponentDeclaringADifferentlyShapedOnMounted_KeepsTheRootWrapperUsable()
    {
        // [CMP-32] collision story, case 1 — the common one. C# hides a base method only by IDENTICAL
        // signature, so a component's own parameterless OnMounted is an ordinary overload: it compiles
        // without a hiding warning and the inherited OnMounted(Action) stays in the candidate set.
        RecordingLifecycle lifecycle = new();
        OverloadingTemplate template = new(new StubContext(lifecycle));

        template.CallOwnThenRegister(static () => { });

        template.OwnInvocationCount.ShouldBe(1);
        lifecycle.Registrations.Select(registration => registration.Hook)
            .ShouldBe(["OnMounted(Action)"]);
    }

    [Fact]
    public void ComponentDeclaringTheSameOnMounted_HidesTheWrapper_AndTheContextFormStillRegisters()
    {
        // [CMP-32] collision story, case 2 — the authored member wins and NOTHING is registered through
        // it. C# reports the hiding as CS0108 (a warning, never an error) unless `new` is written, which
        // is what makes the outcome benign; `new` is written here so this repository's zero-warning build
        // still holds while the resolution being pinned is identical. The escape hatch is the context
        // form, which no member of the component can hide.
        RecordingLifecycle lifecycle = new();
        HidingTemplate template = new(new StubContext(lifecycle));
        Action hidden = static () => { };
        Action registered = static () => { };

        template.RegisterAtRoot(hidden);
        template.RegisterThroughContext(registered);

        template.Captured.ShouldBe([hidden]);
        lifecycle.Registrations.ShouldBe([("OnMounted(Action)", (object)registered)]);
    }

    private static string Signature(MethodInfo method) =>
        $"{method.Name}({string.Join(",", method.GetParameters().Select(parameter => parameter.ParameterType.Name))})";

    /// <summary>The four callback shapes the registration surface accepts, as stable instances.</summary>
    private sealed class LifecycleCallbacks
    {
        internal Action Synchronous { get; } = static () => { };

        internal Func<Task> Asynchronous { get; } = static () => Task.CompletedTask;

        internal Func<CancellationToken, Task> AsynchronousWithToken { get; } =
            static _ => Task.CompletedTask;

        internal Func<Exception, IComponentContext?, string, bool> ErrorCaptured { get; } =
            static (_, _, _) => true;
    }

    /// <summary>Records every registration in order instead of storing callbacks by phase.</summary>
    private sealed class RecordingLifecycle : IComponentLifecycle
    {
        private readonly List<(string Hook, object Callback)> _registrations = [];

        internal IReadOnlyList<(string Hook, object Callback)> Registrations => _registrations;

        public CancellationToken CancellationToken => CancellationToken.None;

        /// <summary>Runs every synchronous callback recorded for one hook, in registration order.</summary>
        /// <param name="hook">The hook key, e.g. <c>OnMounted(Action)</c>.</param>
        internal void InvokeSynchronous(string hook)
        {
            foreach ((string recordedHook, object callback) in _registrations)
            {
                if (string.Equals(recordedHook, hook, StringComparison.Ordinal))
                {
                    ((Action)callback)();
                }
            }
        }

        public void OnBeforeMount(Action callback) => Add("OnBeforeMount(Action)", callback);

        public void OnBeforeMount(Func<Task> callback) => Add("OnBeforeMount(Func`1)", callback);

        public void OnBeforeMount(Func<CancellationToken, Task> callback) =>
            Add("OnBeforeMount(Func`2)", callback);

        public void OnMounted(Action callback) => Add("OnMounted(Action)", callback);

        public void OnMounted(Func<Task> callback) => Add("OnMounted(Func`1)", callback);

        public void OnMounted(Func<CancellationToken, Task> callback) =>
            Add("OnMounted(Func`2)", callback);

        public void OnBeforeUpdate(Action callback) => Add("OnBeforeUpdate(Action)", callback);

        public void OnBeforeUpdate(Func<Task> callback) => Add("OnBeforeUpdate(Func`1)", callback);

        public void OnBeforeUpdate(Func<CancellationToken, Task> callback) =>
            Add("OnBeforeUpdate(Func`2)", callback);

        public void OnUpdated(Action callback) => Add("OnUpdated(Action)", callback);

        public void OnUpdated(Func<Task> callback) => Add("OnUpdated(Func`1)", callback);

        public void OnUpdated(Func<CancellationToken, Task> callback) =>
            Add("OnUpdated(Func`2)", callback);

        public void OnBeforeUnmount(Action callback) => Add("OnBeforeUnmount(Action)", callback);

        public void OnBeforeUnmount(Func<Task> callback) => Add("OnBeforeUnmount(Func`1)", callback);

        public void OnBeforeUnmount(Func<CancellationToken, Task> callback) =>
            Add("OnBeforeUnmount(Func`2)", callback);

        public void OnUnmounted(Action callback) => Add("OnUnmounted(Action)", callback);

        public void OnUnmounted(Func<Task> callback) => Add("OnUnmounted(Func`1)", callback);

        public void OnUnmounted(Func<CancellationToken, Task> callback) =>
            Add("OnUnmounted(Func`2)", callback);

        public void OnErrorCaptured(Func<Exception, IComponentContext?, string, bool> callback) =>
            Add("OnErrorCaptured(Func`4)", callback);

        public void OnServerPrefetch(Func<Task> callback) => Add("OnServerPrefetch(Func`1)", callback);

        public void OnServerPrefetch(Func<CancellationToken, Task> callback) =>
            Add("OnServerPrefetch(Func`2)", callback);

        public void OnActivated(Action callback) => Add("OnActivated(Action)", callback);

        public void OnActivated(Func<Task> callback) => Add("OnActivated(Func`1)", callback);

        public void OnActivated(Func<CancellationToken, Task> callback) =>
            Add("OnActivated(Func`2)", callback);

        public void OnDeactivated(Action callback) => Add("OnDeactivated(Action)", callback);

        public void OnDeactivated(Func<Task> callback) => Add("OnDeactivated(Func`1)", callback);

        public void OnDeactivated(Func<CancellationToken, Task> callback) =>
            Add("OnDeactivated(Func`2)", callback);

        private void Add(string hook, object callback) => _registrations.Add((hook, callback));
    }

    /// <summary>A context that exposes only the recording lifecycle; nothing else is exercised here.</summary>
    private sealed class StubContext(IComponentLifecycle lifecycle) : IComponentContext
    {
        public IComponentArguments Arguments { get; } = new ComponentArguments();

        public IReadOnlyDictionary<string, ComponentSlot> Slots { get; } =
            new Dictionary<string, ComponentSlot>(StringComparer.Ordinal);

        public IComponentAttributeCollection Attributes { get; } = new ComponentAttributes([]);

        public IComponentFactory Components => null!;

        public IServiceProvider Services => null!;

        public IComponentLifecycle Lifecycle { get; } = lifecycle;

        public void Emit(string eventName, params object?[] arguments)
        {
        }
    }

    /// <summary>Reaches the protected surface from the test, in one fixed order per form.</summary>
    private sealed class ProbeTemplate : ComponentTemplateBase
    {
        internal ProbeTemplate(IComponentContext context) => Context = context;

        internal void RegisterEveryRootForm(LifecycleCallbacks callbacks)
        {
            OnBeforeMount(callbacks.Synchronous);
            OnBeforeMount(callbacks.Asynchronous);
            OnBeforeMount(callbacks.AsynchronousWithToken);
            OnMounted(callbacks.Synchronous);
            OnMounted(callbacks.Asynchronous);
            OnMounted(callbacks.AsynchronousWithToken);
            OnBeforeUpdate(callbacks.Synchronous);
            OnBeforeUpdate(callbacks.Asynchronous);
            OnBeforeUpdate(callbacks.AsynchronousWithToken);
            OnUpdated(callbacks.Synchronous);
            OnUpdated(callbacks.Asynchronous);
            OnUpdated(callbacks.AsynchronousWithToken);
            OnBeforeUnmount(callbacks.Synchronous);
            OnBeforeUnmount(callbacks.Asynchronous);
            OnBeforeUnmount(callbacks.AsynchronousWithToken);
            OnUnmounted(callbacks.Synchronous);
            OnUnmounted(callbacks.Asynchronous);
            OnUnmounted(callbacks.AsynchronousWithToken);
            OnActivated(callbacks.Synchronous);
            OnActivated(callbacks.Asynchronous);
            OnActivated(callbacks.AsynchronousWithToken);
            OnDeactivated(callbacks.Synchronous);
            OnDeactivated(callbacks.Asynchronous);
            OnDeactivated(callbacks.AsynchronousWithToken);
            OnErrorCaptured(callbacks.ErrorCaptured);
            OnServerPrefetch(callbacks.Asynchronous);
            OnServerPrefetch(callbacks.AsynchronousWithToken);
        }

        internal void RegisterEveryContextForm(LifecycleCallbacks callbacks)
        {
            Context.Lifecycle.OnBeforeMount(callbacks.Synchronous);
            Context.Lifecycle.OnBeforeMount(callbacks.Asynchronous);
            Context.Lifecycle.OnBeforeMount(callbacks.AsynchronousWithToken);
            Context.Lifecycle.OnMounted(callbacks.Synchronous);
            Context.Lifecycle.OnMounted(callbacks.Asynchronous);
            Context.Lifecycle.OnMounted(callbacks.AsynchronousWithToken);
            Context.Lifecycle.OnBeforeUpdate(callbacks.Synchronous);
            Context.Lifecycle.OnBeforeUpdate(callbacks.Asynchronous);
            Context.Lifecycle.OnBeforeUpdate(callbacks.AsynchronousWithToken);
            Context.Lifecycle.OnUpdated(callbacks.Synchronous);
            Context.Lifecycle.OnUpdated(callbacks.Asynchronous);
            Context.Lifecycle.OnUpdated(callbacks.AsynchronousWithToken);
            Context.Lifecycle.OnBeforeUnmount(callbacks.Synchronous);
            Context.Lifecycle.OnBeforeUnmount(callbacks.Asynchronous);
            Context.Lifecycle.OnBeforeUnmount(callbacks.AsynchronousWithToken);
            Context.Lifecycle.OnUnmounted(callbacks.Synchronous);
            Context.Lifecycle.OnUnmounted(callbacks.Asynchronous);
            Context.Lifecycle.OnUnmounted(callbacks.AsynchronousWithToken);
            Context.Lifecycle.OnActivated(callbacks.Synchronous);
            Context.Lifecycle.OnActivated(callbacks.Asynchronous);
            Context.Lifecycle.OnActivated(callbacks.AsynchronousWithToken);
            Context.Lifecycle.OnDeactivated(callbacks.Synchronous);
            Context.Lifecycle.OnDeactivated(callbacks.Asynchronous);
            Context.Lifecycle.OnDeactivated(callbacks.AsynchronousWithToken);
            Context.Lifecycle.OnErrorCaptured(callbacks.ErrorCaptured);
            Context.Lifecycle.OnServerPrefetch(callbacks.Asynchronous);
            Context.Lifecycle.OnServerPrefetch(callbacks.AsynchronousWithToken);
        }

        internal void RegisterMountedAtRoot(Action callback) => OnMounted(callback);

        internal void RegisterMountedThroughContext(Action callback) =>
            Context.Lifecycle.OnMounted(callback);
    }

    /// <summary>A component whose own <c>OnMounted</c> has a different signature — an overload, not hiding.</summary>
    private sealed class OverloadingTemplate : ComponentTemplateBase
    {
        internal OverloadingTemplate(IComponentContext context) => Context = context;

        internal int OwnInvocationCount { get; private set; }

        internal void CallOwnThenRegister(Action callback)
        {
            OnMounted();        // the component's own member
            OnMounted(callback); // the inherited root wrapper
        }

        private void OnMounted() => OwnInvocationCount++;
    }

    /// <summary>A component whose own <c>OnMounted(Action)</c> hides the inherited wrapper.</summary>
    private sealed class HidingTemplate : ComponentTemplateBase
    {
        internal HidingTemplate(IComponentContext context) => Context = context;

        internal List<Action> Captured { get; } = [];

        internal void RegisterAtRoot(Action callback) => OnMounted(callback);

        internal void RegisterThroughContext(Action callback) =>
            Context.Lifecycle.OnMounted(callback);

        private new void OnMounted(Action callback) => Captured.Add(callback);
    }
}
