using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Provides a host-neutral warning channel for runtime helpers operating on a component context.
/// </summary>
/// <remarks>
/// Core-created <see cref="IComponentContext"/> instances implement this capability and forward
/// warnings to the owning application's configured warning handler. Host libraries can test for
/// this interface without depending on application internals.
/// </remarks>
public interface IComponentWarningContext
{
    /// <summary>Reports a non-fatal runtime warning.</summary>
    /// <param name="message">The warning message.</param>
    void Warn(string message);
}
