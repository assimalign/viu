# Assimalign.Viu.Testing.EndToEnd

This test-only executable drives Viu exclusively from the browser side through Playwright .NET. It
is under `benchmarks/` because browser orchestration, trace capture, startup measurement, and the
loopback static server form Viu's executable performance and end-to-end verification harness. It is never
packed and no shipping library references it. Purpose-built package-consumer applications remain
under `scripts/fixtures/`, beside the script that stages them.

`scripts/Test-EndToEnd.ps1` is the supported entry point. It packs the current Viu SDK/framework
set, creates an isolated external-consumer restore boundary, publishes trimmed Browser and
hydration applications, generates hydration markup through the packaged
`ServerRenderAdaptor<TContext>`, installs the selected Playwright browsers, serves both published
`wwwroot` trees, and runs the same scenarios in Chromium, Firefox, and WebKit.

Scenarios cover clean boot, router navigation with manual saved-position restoration, click and
input dispatch, a `v-model` round trip,
keyed DOM moves, component unmount cleanup with Browser node and listener-registry recovery,
scheduler/`NextTickAsync` ordering, Browser adoption of server-rendered nodes, and visible lazy
hydration through `IntersectionObserver`. A failed scenario records its engine and name and retains
a full-page screenshot plus Playwright trace. Unexpected browser console/page errors and failed
network requests fail that scenario.

Startup mode performs one or more warm-up loads followed by at least ten fresh-context
boot-to-interactive measurements in Chromium. A sample stops only after the increment click is
acknowledged by the reactive count, proving event dispatch and update readiness. Its JSON result is
the machine-readable input to the scheduled startup-budget lane. The separately exposed
publish-only mode reuses exactly the same isolated package-consumer preparation for trimmed-size
and AOT budget lanes. Its `-PackagedVuePublish` selection publishes a `.vue`-only Browser fixture
through the installed SDK/framework packages, requires a non-empty component-CSS bundle,
and scans both generated sources and the published consumer assembly for absent Debug-only
hot-reload metadata in Release and AOT output.

The separate `-HotReload` mode stages one package-only Debug Browser application and starts it under
an owned `dotnet watch` process. Its mounted root is a tag-based `.vue` component. The Chromium
scenarios prove add-element, remove-element, and `v-if` template edits apply as managed deltas,
remount only the affected component, and preserve the connected document. Adding a new canonical
`.viu` file exercises the runtime's `NewTypeDefinition` path without remounting the unrelated root.
The lane also pins component-stylesheet replacement without document navigation, semantic no-op
suppression without a managed or static-asset update, affected-component remount for accepted
template and C# script-body changes, and automatic pinned-origin browser reload only after a rejected
script-signature edit. The harness terminates the complete process tree it started and verifies that
the CSS worker process did not survive. This opt-in mode never changes the ordinary
three-scenario-per-engine matrix. These Phase 2 guarantees are specified by [V01.01.06.14], #350,
and [SFC-CG-4].
