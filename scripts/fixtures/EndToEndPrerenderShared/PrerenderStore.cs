#if STATIC_PRERENDER_HOST
namespace EndToEndPrerenderHost;
#else
namespace EndToEndPrerenderApp;
#endif

internal sealed class PrerenderStore
{
    internal PrerenderState State { get; set; } = new();
}
