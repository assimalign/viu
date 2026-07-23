using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>Handles all arguments emitted by a child component synchronously.</summary>
/// <param name="arguments">The ordered emitted arguments.</param>
public delegate void ComponentEventArgumentsHandler(IReadOnlyList<object?> arguments);
