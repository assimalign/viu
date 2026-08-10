using System;
using System.Text.Json;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Validates and publishes one exact semantic-classification notification received from the Viu
/// language server.
/// </summary>
internal sealed class ViuSemanticClassificationReceiver
{
    /// <summary>The custom notification method published by the Viu language server.</summary>
    internal const string PublicationMethodName = "viu/publishSemanticClassifications";

    private readonly ViuSemanticClassificationState state;

    /// <summary>Initializes the receiver over the process-wide classification state.</summary>
    internal ViuSemanticClassificationReceiver(ViuSemanticClassificationState state) =>
        this.state = state ?? throw new ArgumentNullException(nameof(state));

    /// <summary>Publishes a valid notification and rejects malformed input without changing state.</summary>
    internal bool Receive(JsonElement parameters)
    {
        if (!ViuSemanticClassificationPublication.TryParse(parameters, out var publication))
        {
            return false;
        }

        return this.state.Publish(publication!);
    }
}
