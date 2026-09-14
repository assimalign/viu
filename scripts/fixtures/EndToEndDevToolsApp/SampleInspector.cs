using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.DevTools;

namespace EndToEndDevToolsApp;

// The client has no knowledge of this provider or its field names. [DVT-7], [DVT-14].
internal sealed class SampleInspector : IDevToolsInspector
{
    public string Identifier => "sample-inventory";

    public string DisplayName => "Sample inventory";

    public ValueTask<IReadOnlyList<DevToolsInspectorNode>> GetTreeAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IReadOnlyList<DevToolsInspectorNode>>(
            [new DevToolsInspectorNode("warehouse", "Sample warehouse",
                [new DevToolsInspectorNode("shelf", "Sample shelf")])]);
    }

    public ValueTask<IReadOnlyDictionary<string, object?>> GetStateAsync(
        string nodeIdentifier,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IReadOnlyDictionary<string, object?>>(
            new Dictionary<string, object?>
            {
                ["location"] = nodeIdentifier == "warehouse" ? "Warehouse north" : "Shelf one",
                ["available"] = 12,
            });
    }
}
