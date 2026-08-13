using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu;

/// <summary>Loads the registered identity of an asynchronous component.</summary>
/// <remarks>
/// The result is a registration identity, never an activated instance, so activation remains
/// explicit and trimming-safe. Specified by <c>[BLT-14]</c>.
/// </remarks>
/// <param name="cancellationToken">Cancels a shared load after its last consumer releases it.</param>
/// <returns>The registered component identity produced by the load.</returns>
public delegate Task<AsynchronousComponentTarget> AsynchronousComponentLoader(
    CancellationToken cancellationToken);
