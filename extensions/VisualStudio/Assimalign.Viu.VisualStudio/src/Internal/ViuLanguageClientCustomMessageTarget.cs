using System;
using System.Text.Json;

using StreamJsonRpc;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Exposes Viu's server-to-client semantic-classification notification to StreamJsonRpc.
/// </summary>
internal sealed class ViuLanguageClientCustomMessageTarget
{
    private readonly ViuSemanticClassificationReceiver semanticClassificationReceiver;

    /// <summary>Initializes the target over the editor-free notification receiver.</summary>
    internal ViuLanguageClientCustomMessageTarget(
        ViuSemanticClassificationReceiver semanticClassificationReceiver)
    {
        this.semanticClassificationReceiver = semanticClassificationReceiver ??
            throw new ArgumentNullException(nameof(semanticClassificationReceiver));
    }

    /// <summary>Receives one exact semantic-classification publication from the language server.</summary>
    /// <param name="parameters">The single JSON object carried by the custom notification.</param>
    [JsonRpcMethod(
        ViuSemanticClassificationReceiver.PublicationMethodName,
        UseSingleObjectParameterDeserialization = true)]
    public void PublishSemanticClassifications(JsonElement parameters) =>
        this.semanticClassificationReceiver.Receive(parameters);
}
