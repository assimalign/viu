using System;
using System.Collections.Generic;

namespace Assimalign.Viu;

/// <summary>
/// Optional abstract base for authoring an <see cref="IComponent"/> with C# ergonomics: it turns
/// the composition-API primitives into protected factory helpers (<see cref="Reference{T}(T)"/>,
/// <see cref="Computed{T}(Func{T}, Action{T}?)"/>, …) and exposes a fluent
/// <see cref="Configure(IComponentDescriptor)"/> seam for declaring props and emits. Deriving from
/// this base is authoring sugar only — implementing <see cref="IComponent"/> directly is equally
/// supported. Mirrors upstream's <c>defineComponent</c> convenience over the raw options object
/// (<c>packages/runtime-core/src/apiDefineComponent.ts</c>).
/// </summary>
/// <remarks>
/// Not thread-safe: like the rest of the runtime it targets the single-threaded JS event loop.
/// </remarks>
public abstract class Component : IComponent
{
    private readonly List<ComponentPropertyDefinition> _properties = new();
    private readonly List<ComponentEmitDefinition> _emits = new();
    private bool _configured;

    /// <summary>
    /// The component's display name for warnings and devtools (upstream: the <c>name</c> option),
    /// or null. Declared virtual on the base — rather than left to the <see cref="IComponent"/>
    /// default member — so a derived author can <c>override</c> it and have the runtime, which reads
    /// <see cref="IComponent.Name"/>, observe the override.
    /// </summary>
    public virtual string? Name => null;

    /// <summary>
    /// Whether undeclared attributes fall through to a single element root (upstream:
    /// <c>inheritAttrs</c>, default true). Override to opt out.
    /// </summary>
    public virtual bool InheritAttributes => true;

    /// <summary>
    /// The declared props, materialized on first access by running <see cref="Configure"/> once
    /// (upstream: the <c>props</c> option).
    /// </summary>
    public virtual IReadOnlyList<ComponentPropertyDefinition> Properties
    {
        get
        {
            EnsureConfigured();
            return _properties.AsReadOnly();
        }
    }

    /// <summary>
    /// The declared emitted events, materialized on first access by running <see cref="Configure"/>
    /// once (upstream: the <c>emits</c> option).
    /// </summary>
    public virtual IReadOnlyList<ComponentEmitDefinition> Emits
    {
        get
        {
            EnsureConfigured();
            return _emits.AsReadOnly();
        }
    }

    /// <summary>
    /// Registers this component's props and emits. Called lazily exactly once, on the first access
    /// to <see cref="Properties"/> or <see cref="Emits"/> — deliberately NOT from the constructor,
    /// because a virtual call during construction would run a derived override before the derived
    /// constructor body finished. Definition-time metadata only; lifecycle hooks belong in
    /// <see cref="Setup"/> (ADR-0004, composition-only).
    /// </summary>
    /// <param name="descriptor">The fluent registration surface for props and emits.</param>
    protected virtual void Configure(IComponentDescriptor descriptor)
    {
    }

    /// <summary>
    /// The Composition API entry point (upstream: <c>setup(props, context)</c>): runs once per
    /// instance and returns the render function that re-executes per update.
    /// </summary>
    /// <param name="properties">The instance's shallow-reactive props.</param>
    /// <param name="context">Attrs, Emit, Expose, and Slots.</param>
    /// <returns>The render function producing the component's subtree.</returns>
    public abstract ComponentSetup Setup(ComponentProperties properties, ComponentSetupContext context);

    /// <summary>Creates a reactive ref holding <paramref name="value"/> (Vue's <c>ref()</c>).</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The initial value.</param>
    /// <returns>The new ref.</returns>
    protected Reference<T> Reference<T>(T value)
    {
        return new Reference<T>(value);
    }

    /// <summary>Creates a shallow ref holding <paramref name="value"/> (Vue's <c>shallowRef()</c>).</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The initial value.</param>
    /// <returns>The new shallow ref.</returns>
    protected ShallowReference<T> ShallowReference<T>(T value)
    {
        return new ShallowReference<T>(value);
    }

    /// <summary>Creates a custom ref with explicit track/trigger control (Vue's <c>customRef()</c>).</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="factory">Receives track/trigger delegates and returns the getter/setter pair.</param>
    /// <returns>The new custom ref.</returns>
    protected CustomReference<T> CustomReference<T>(CustomReferenceFactory<T> factory)
    {
        return new CustomReference<T>(factory);
    }

    /// <summary>
    /// Creates a lazy cached computed over <paramref name="getter"/>; pass <paramref name="setter"/>
    /// for the writable variant (Vue's <c>computed()</c>).
    /// </summary>
    /// <typeparam name="T">The computed value type.</typeparam>
    /// <param name="getter">The derivation function (not invoked until the first read).</param>
    /// <param name="setter">Optional setter making the computed writable.</param>
    /// <returns>The new computed.</returns>
    protected Computed<T> Computed<T>(Func<T> getter, Action<T>? setter = null)
    {
        return new Computed<T>(getter, setter);
    }

    /// <summary>
    /// Creates a reactive effect over <paramref name="action"/>, runs it immediately, and returns
    /// the runner handle (Vue's <c>effect()</c>). With a <paramref name="scheduler"/>, later
    /// invalidations invoke the scheduler instead of re-running the effect. If the first run
    /// throws, the effect is stopped before the exception propagates (upstream <c>effect()</c>
    /// parity), so a failed effect leaves no live subscriptions behind.
    /// </summary>
    /// <param name="action">The reactive function to track.</param>
    /// <param name="scheduler">Optional scheduler invoked on invalidation instead of a re-run.</param>
    /// <returns>The effect handle (use <see cref="ReactiveEffect.Run"/>/<see cref="ReactiveEffect.Stop"/>).</returns>
    protected ReactiveEffect Effect(Action action, Action? scheduler = null)
    {
        var effect = new ReactiveEffect(action) { Scheduler = scheduler };
        try
        {
            effect.Run();
        }
        catch
        {
            effect.Stop();
            throw;
        }
        return effect;
    }

    /// <summary>Creates an effect scope (Vue's <c>effectScope()</c>).</summary>
    /// <param name="detached">When true, the scope does not attach to the current scope.</param>
    /// <returns>The new scope.</returns>
    protected EffectScope EffectScope(bool detached = false)
    {
        return new EffectScope(detached);
    }

    /// <summary>
    /// Runs <see cref="Configure"/> the first time component metadata is read. The runtime is
    /// single-threaded (JS event loop), so a plain bool guard is sufficient — no locking. The
    /// guard is set before the call so a re-entrant metadata read from within
    /// <see cref="Configure"/> cannot recurse.
    /// </summary>
    private void EnsureConfigured()
    {
        if (_configured)
        {
            return;
        }
        _configured = true;
        Configure(new ComponentDescriptor(this));
    }

    private sealed class ComponentDescriptor : IComponentDescriptor
    {
        public ComponentDescriptor(Component component)
        {
            Component = component;
        }

        public Component Component { get; }

        public IComponentDescriptor WithEmit(ComponentEmitDefinition emit)
        {
            ArgumentNullException.ThrowIfNull(emit);
            Component._emits.Add(emit);
            return this;
        }

        public IComponentDescriptor WithProperty(ComponentPropertyDefinition property)
        {
            ArgumentNullException.ThrowIfNull(property);
            Component._properties.Add(property);
            return this;
        }
    }
}
