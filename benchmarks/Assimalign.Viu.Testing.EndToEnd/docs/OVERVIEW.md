# Assimalign.Viu.Testing.EndToEnd

This test-only executable drives Viu exclusively from the browser side through Playwright .NET. It
is a test harness, not a benchmark. It sits under `benchmarks/` for historical reasons — the
performance benchmark suite that once shared the folder now lives in the sibling
`viu-benchmarks` repository, and browser orchestration, trace capture, startup measurement, and the
loopback static server stayed here because they need this repository's source. It is never
packed and no shipping library references it. Purpose-built package-consumer applications remain
under `scripts/fixtures/`, beside the script that stages them.

`scripts/Test-EndToEnd.ps1` is the supported entry point. It packs the current Viu SDK/framework
set, creates an isolated external-consumer restore boundary, publishes trimmed Browser,
hydration, and static prerender applications, generates hydration markup through the packaged
`ServerRenderAdaptor<TContext>`, installs the selected Playwright browsers, serves the published
`wwwroot` trees, and runs the same scenarios in Chromium, Firefox, and WebKit.

Scenarios cover clean boot, router navigation with manual saved-position restoration, click and
input dispatch, a `v-model` round trip,
keyed DOM moves, component unmount cleanup with Browser node and listener-registry recovery,
scheduler/`NextTickAsync` ordering, Browser adoption of server-rendered nodes, and visible lazy
hydration through `IntersectionObserver`. A failed scenario records its engine and name and retains
a full-page screenshot plus Playwright trace. Unexpected browser console/page errors and failed
network requests fail that scenario.

The two static prerender scenarios publish `EndToEndPrerenderApp` with
`ViuStaticPrerender=true`. The packaged base SDK builds and invokes the package-only server host,
which emits `/` and `/guide/intro` from the fingerprinted published host page. The script checks
both files for hydration markers and a state island. Each browser navigates directly to one emitted
route, verifies the original heading identity and server-provided state, and observes zero DOM
mutations during hydration except the required state-island removal ([HYD-4], [HYD-8]). A subsequent
click proves the adopted button is interactive. The shared explicit component registrations use
`RouterView`, a parameterized nested route, a navigation guard, and a source-generated JSON context;
the server creates a new application, router, and state registry for each route. Specified by
[V01.01.07.05], #68.

The opt-in `-DevTools` mode packs the ecosystem inspection-client library and publishes
`scripts/fixtures/EndToEndDevToolsApp`. Its Chromium scenario uses an iframe with an independent
WebAssembly runtime for the Viu panel and exchanges only postMessage protocol frames with the
inspected application. It checks incremental mounts and unmounts, lazy snapshot expansion,
state edits visible in the inspected DOM, correlated timeline highlighting, generic custom
inspector rendering, and a fresh handshake after document reload ([DVT-13], [DVT-14]). Combine
`-DevTools -PublishOnly -PublishDirectory _out/devtools-publish` to retain only the trimmed host.

Startup mode performs one or more warm-up loads followed by at least ten fresh-context
boot-to-interactive measurements in Chromium. A sample stops only after the increment click is
acknowledged by the reactive count, proving event dispatch and update readiness. Its JSON result is
the machine-readable input to the scheduled startup-budget lane, which is enforced from the sibling
`viu-benchmarks` repository (`scripts/Test-StartupBudget.ps1`). The separately exposed
publish-only mode reuses exactly the same isolated package-consumer preparation for that
repository's trimmed-size
and AOT budget lanes. Its `-PackagedVuePublish` selection publishes a `.vue`-only Browser fixture
through the installed SDK/framework packages, requires a non-empty component-CSS bundle,
and scans both generated sources and the published consumer assembly for absent Debug-only
hot-reload metadata in Release and AOT output.

The separate `-HotReload` mode stages one package-only Debug Browser application. Before starting
watch, it performs an ordinary Debug build and launches the application through the packaged
RunHost with a protocol-asserting Visual Studio-shaped BrowserRefresh endpoint. A real Chromium page
connects to RunHost's rewritten loopback websocket; the harness verifies the encrypted-secret
upstream handshake, a bidirectional capability request, targeted component and utility
`UpdateStaticFile` messages from one `.viu` edit, both stylesheet replacements, and retained
document identity. A second Visual Studio-shaped scenario sends an upstream `Reload` before another
regeneration completes, gives the replacement document stale CSS, withholds its refresh client until
the worker reports completion, and then requires connect-time synchronization to converge both
stylesheets without manual action. This coverage is specified by [V01.01.12.30.05], #357.

The mode then runs the unchanged nine scenarios under owned `dotnet watch` processes. Its mounted
root is a tag-based `.vue` component. The Chromium scenarios prove add-element, remove-element, and
`v-if` template edits apply as managed deltas,
remount only the affected component, and preserve the connected document. Adding a new canonical
`.viu` file exercises the runtime's `NewTypeDefinition` path without remounting the unrelated root.
The lane also pins component-stylesheet replacement without document navigation, semantic no-op
suppression without a managed or static-asset update, affected-component remount for accepted
template and C# script-body changes, and automatic pinned-origin browser reload only after a rejected
script-signature edit. The harness terminates the complete process tree it started and verifies that
the CSS worker process did not survive. The same staged consumer privately references the packaged
`Assimalign.Viu.UtilityCss.Build` add-on and authors its utility stylesheet link. Its final two
connected-document scenarios add a never-before-generated utility class to the mounted template,
observe the regenerated stylesheet and Chromium computed style without a restart, then delete the
last source contributing a utility rule and observe the empty retirement update without a managed
delta or state loss. This generated-asset coverage is specified by [V01.01.12.30.04], #355.
This opt-in mode never changes the ordinary five-scenario-per-engine matrix. These Phase 2
guarantees are specified by [V01.01.06.14], #350, and [SFC-CG-4].
