using System;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>Creates the tree shown for an asynchronous-component load failure.</summary>
/// <remarks>Specified by <c>[BLT-14]</c>.</remarks>
/// <param name="error">The loader or timeout failure.</param>
/// <returns>A fresh immutable failure tree.</returns>
public delegate VirtualNode? AsynchronousComponentErrorRenderer(Exception error);
