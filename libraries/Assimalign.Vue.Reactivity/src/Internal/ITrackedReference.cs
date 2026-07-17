namespace Assimalign.Vue.Reactivity;

/// <summary>
/// Internal contract for reactive sources that own a <see cref="Reactivity.Dependency"/> directly —
/// refs and computeds — enabling <see cref="Reactive.TriggerReference"/> to force-notify without
/// knowing the concrete type.
/// </summary>
internal interface ITrackedReference
{
    /// <summary>The dependency tracking reads of the source's value cell.</summary>
    Dependency Dependency { get; }
}
