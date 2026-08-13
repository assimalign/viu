using System;

namespace Assimalign.Viu;

/// <summary>
/// Selects the ambient runtime bookkeeping used by one logical execution flow.
/// </summary>
/// <remarks>
/// Browser applications use the shared single-event-loop flow. Request-oriented hosts enter a
/// fresh flow so Core, Reactivity, and State bookkeeping cannot leak between concurrent requests.
/// The returned lease restores the previously selected flow when disposed and is not thread-safe.
/// Specified by <c>[EXE-1]</c>.
/// </remarks>
public static class RuntimeExecution
{
    /// <summary>
    /// Enters a fresh logical execution flow for Core, Reactivity, and State, returning an
    /// idempotent restoration lease. Nested flows restore their immediate predecessor in
    /// last-in-first-out order. Specified by <c>[EXE-1]</c>.
    /// </summary>
    /// <returns>A lease that restores the previously selected execution flow.</returns>
    public static IDisposable EnterExecutionFlow() => CoreExecutionIsolation.Enter();
}
