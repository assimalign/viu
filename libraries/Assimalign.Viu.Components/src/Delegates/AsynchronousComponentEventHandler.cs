using System.Threading.Tasks;

namespace Assimalign.Viu.Components;

/// <summary>Handles the first value emitted by a child component asynchronously.</summary>
/// <param name="value">The first emitted value, or null when the event has no arguments.</param>
/// <returns>The task representing the handler invocation.</returns>
public delegate Task AsynchronousComponentEventHandler(object? value);
