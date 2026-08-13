using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// Receives one authored-component event emission.
/// </summary>
/// <remarks>Delivery is synchronous and specified by <c>[CMP-15]</c>.</remarks>
/// <param name="arguments">The immutable event argument snapshot.</param>
public delegate void ComponentEventListener(IReadOnlyList<object?> arguments);
