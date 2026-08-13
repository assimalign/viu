using System.Text.Json;
using System.Text.Json.Serialization;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>Provides reflection-free metadata for raw state-island normalization.</summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class SsrStateIslandJsonSerializerContext : JsonSerializerContext;
