using System.Text.Json.Serialization;

namespace Assimalign.Viu.Testing.EndToEnd;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(HarnessResultSummary))]
[JsonSerializable(typeof(ScenarioResult))]
[JsonSerializable(typeof(StartupMeasurementResult))]
internal sealed partial class HarnessJSONSerializerContext : JsonSerializerContext
{
}
