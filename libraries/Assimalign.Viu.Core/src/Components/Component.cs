using System;
using System.Collections.Generic;

namespace Assimalign.Viu;

public abstract class Component : IComponent
{
    private readonly List<ComponentPropertyDefinition> _properties = new();
    private readonly List<ComponentEmitDefinition> _emits = new();


    protected Component()
    {
        Configure(new ComponentDescirptor(this));
    }

    public virtual bool InheritAttrs => true;
    public virtual IReadOnlyList<ComponentPropertyDefinition> Properties => _properties.AsReadOnly();
    public virtual IReadOnlyList<ComponentEmitDefinition> Emits => _emits.AsReadOnly();

    /// <summary>
    /// Configures the component on constructor initialization. This is the place to register emits and properties for the component.
    /// </summary>
    /// <param name="descriptor"></param>
    protected virtual void Configure(IComponentDescriptor descriptor)
    {

    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="properties"></param>
    /// <param name="context"></param>
    /// <returns></returns>
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


    private partial class ComponentDescirptor : IComponentDescriptor
    {
        public ComponentDescirptor(Component component)
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