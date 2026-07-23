using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>Creates the component tree shown for an asynchronous-component failure.</summary>
/// <param name="error">The loader or timeout failure.</param>
/// <returns>A fresh render-tree value for the failure.</returns>
public delegate IComponent AsynchronousComponentErrorRenderer(Exception error);
