using System;
using System.Threading.Tasks;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

internal interface IAsynchronousComponentRuntime
{
    ComponentInvocation Invocation { get; }

    MountReference? MountReference { get; }

    bool RegisterAsynchronousDependency(Task dependency);

    void SettleAsynchronousDependency(Task dependency);

    void RouteAsynchronousError(Exception exception, bool rethrowIfUnhandled);
}
