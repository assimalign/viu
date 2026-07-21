using System.Text.Json;
using System.Text.Json.Serialization;

namespace Assimalign.Viu.Testing.Benchmarks;

/// <summary>
/// The System.Text.Json source-generation context for the baseline manifest and the results document.
/// Source generation (not reflection) keeps the harness trimming/AOT-safe and lets the same camelCase
/// contract read the checked-in baseline and write the per-run results.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(InteropCountBaseline))]
[JsonSerializable(typeof(InteropCountResultsDocument))]
internal sealed partial class InteropCountJsonContext : JsonSerializerContext
{
}
