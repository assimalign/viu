using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu;

internal sealed class AsynchronousComponentLoadState
{
    internal CancellationTokenSource Cancellation { get; } = new();

    internal Task<AsynchronousComponentTarget> PendingLoad { get; set; } = null!;

    internal int ConsumerCount { get; set; }
}
