using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Assimalign.Viu.RuntimeCore.Tests")]
// The compiled-render end-to-end proof ([V01.01.03.22]) resets the scheduler between tests to isolate its
// ambient state, exactly as the sibling render-effect/renderer tests do.
[assembly: InternalsVisibleTo("Assimalign.Viu.RuntimeCore.CompiledRenderTests")]
[assembly: InternalsVisibleTo("Assimalign.Viu.Testing")]
// The server renderer ([V01.01.07.01]) drives the platform-agnostic half of the component lifecycle
// server-side — create instance, run Setup, await ServerPrefetch hooks, render the component root —
// exactly the primitives upstream exposes to @vue/server-renderer through runtime-core's `ssrUtils`
// internal export (createComponentInstance/setupComponent/renderComponentRoot). It carries no
// DOM/interop dependency; this grant is the C# analog of that `@internal` seam, so the SSR assembly
// reuses the real component pipeline instead of forking it.
[assembly: InternalsVisibleTo("Assimalign.Viu.ServerRenderer")]
// The browser package arms Scheduler.FlushBoundaryCallback — the interop command-buffer flush
// seam ([V01.01.04.05]) — which is an internal static hook, not public API surface.
[assembly: InternalsVisibleTo("Assimalign.Viu.RuntimeDom")]
// The RuntimeDom directive tests drive the RuntimeCore scheduler directly (v-model/v-show run
// through the real renderer + post-flush pipeline), so they reset it between tests just as the
// Testing library does per mount ([V01.01.04.06]).
[assembly: InternalsVisibleTo("Assimalign.Viu.RuntimeDom.Tests")]
