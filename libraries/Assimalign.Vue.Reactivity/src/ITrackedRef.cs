namespace Assimalign.Vue.Reactivity;

/// <summary>
/// Internal contract for reactive sources that own a <see cref="Reactivity.Dep"/> directly —
/// refs and computeds — enabling <see cref="Reactive.TriggerRef"/> to force-notify without
/// knowing the concrete type.
/// </summary>
internal interface ITrackedRef
{
    /// <summary>The dep tracking reads of the source's value cell.</summary>
    Dep Dep { get; }
}
