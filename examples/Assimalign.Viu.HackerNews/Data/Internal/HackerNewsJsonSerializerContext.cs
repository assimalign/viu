using System.Text.Json.Serialization;

namespace Assimalign.Viu.HackerNews;

/// <summary>
/// The System.Text.Json <b>source-generated</b> serialization context for the HackerNews wire DTOs —
/// the sanctioned trimming/NativeAOT-safe JSON path for Viu apps, and the pattern this (the first
/// networked sample) establishes.
/// <para>
/// Reflection-based System.Text.Json is forbidden by the repo's AOT rules: the reflection
/// <c>JsonSerializer.Deserialize&lt;T&gt;(string, JsonSerializerOptions)</c> overloads are annotated
/// <c>[RequiresUnreferencedCode]</c>/<c>[RequiresDynamicCode]</c> and would fail the trimmed publish
/// under <c>-warnaserror</c>. Instead, every DTO is registered with <see cref="JsonSerializableAttribute"/>
/// here; the generator emits the metadata at build time on the host, and <see cref="HackerNewsClient"/>
/// deserializes through the generated <c>JsonTypeInfo&lt;T&gt;</c> (<c>Default.ItemPayload</c> /
/// <c>Default.UserPayload</c>) overloads, which carry no reflection/codegen requirement.
/// </para>
/// <para>
/// Only the two object DTOs are registered. The feed endpoints return a bare JSON array of numeric
/// ids, which <see cref="HackerNewsClient"/> reads with <c>JsonDocument</c> — itself reflection-free
/// and trim-safe — so no array metadata is generated. <see cref="JsonSourceGenerationOptionsAttribute.PropertyNameCaseInsensitive"/>
/// binds the PascalCase DTO members to the API's lowercase JSON keys.
/// </para>
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ItemPayload))]
[JsonSerializable(typeof(UserPayload))]
internal partial class HackerNewsJsonSerializerContext : JsonSerializerContext
{
}
