using System;

namespace Assimalign.Viu;

/// <summary>Resolves a runtime directive from its compiler-emitted type token.</summary>
/// <remarks>
/// Resolution is explicit and reflection-free so directive activation remains trimming- and
/// WASM-AOT-safe. Specified by <c>[CMP-7]</c>, <c>[APP-2]</c>, and <c>[SFC-CG-6]</c>.
/// </remarks>
public interface IDirectiveResolver
{
    /// <summary>Resolves a reusable directive, or returns null when the token is not registered.</summary>
    /// <param name="directiveType">The compile-time-known directive type token.</param>
    /// <returns>The registered directive, or null.</returns>
    IDirective? Resolve(Type directiveType);
}
