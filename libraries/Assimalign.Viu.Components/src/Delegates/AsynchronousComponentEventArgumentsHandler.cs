using System.Collections.Generic;
using System.Threading.Tasks;

namespace Assimalign.Viu.Components;

/// <summary>Handles all arguments emitted by a child component asynchronously.</summary>
/// <param name="arguments">The ordered emitted arguments.</param>
/// <returns>The task representing the handler invocation.</returns>
public delegate Task AsynchronousComponentEventArgumentsHandler(
    IReadOnlyList<object?> arguments);
