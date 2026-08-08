using System.Threading.Tasks;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

internal interface IAsynchronousComponentRuntime
{
    ComponentInvocation Invocation { get; }

    MountReference? MountReference { get; }

    bool RegisterAsynchronousDependency(
        Task dependency,
        bool rethrowIfUnhandled);

    void ObserveAsynchronousDependency(
        Task dependency,
        bool rethrowIfUnhandled);
}
