namespace Assimalign.Viu.Components;

/// <summary>Describes an event a component template may emit.</summary>
public interface IComponentEvent
{
    /// <summary>Gets the event name.</summary>
    string Name { get; }
}

