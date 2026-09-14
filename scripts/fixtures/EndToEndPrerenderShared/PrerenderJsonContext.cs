using System.Text.Json.Serialization;

#if STATIC_PRERENDER_HOST
namespace EndToEndPrerenderHost;
#else
namespace EndToEndPrerenderApp;
#endif

[JsonSerializable(typeof(PrerenderState))]
internal sealed partial class PrerenderJsonContext : JsonSerializerContext
{
}
