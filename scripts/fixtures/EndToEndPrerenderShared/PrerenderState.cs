#if STATIC_PRERENDER_HOST
namespace EndToEndPrerenderHost;
#else
namespace EndToEndPrerenderApp;
#endif

internal sealed class PrerenderState
{
    public string Message { get; set; } = "client-default";
}
