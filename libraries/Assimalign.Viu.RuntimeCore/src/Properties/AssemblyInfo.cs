using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Assimalign.Viu.RuntimeCore.Tests")]
// The compiled-render end-to-end proof ([V01.01.03.22]) resets the scheduler between tests to isolate its
// ambient state, exactly as the sibling render-effect/renderer tests do.
[assembly: InternalsVisibleTo("Assimalign.Viu.RuntimeCore.CompiledRenderTests")]
[assembly: InternalsVisibleTo("Assimalign.Viu.Testing")]
// The browser package arms Scheduler.FlushBoundaryCallback — the interop command-buffer flush
// seam ([V01.01.04.05]) — which is an internal static hook, not public API surface.
[assembly: InternalsVisibleTo("Assimalign.Viu.RuntimeDom")]
// The RuntimeDom directive tests drive the RuntimeCore scheduler directly (v-model/v-show run
// through the real renderer + post-flush pipeline), so they reset it between tests just as the
// Testing library does per mount ([V01.01.04.06]).
[assembly: InternalsVisibleTo("Assimalign.Viu.RuntimeDom.Tests")]
