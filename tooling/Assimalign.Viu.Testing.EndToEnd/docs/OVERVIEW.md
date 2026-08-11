# Assimalign.Viu.Testing.EndToEnd

This test-only executable drives Viu exclusively from the browser side through Playwright .NET. It
is under `tooling/` because browser orchestration, trace capture, startup measurement, and the
loopback static server are developer tools rather than reusable runtime-testing APIs. It is never
packed and no shipping library references it. Purpose-built package-consumer applications remain
under `scripts/fixtures/`, beside the script that stages them.

`scripts/Test-EndToEnd.ps1` is the supported entry point. It packs the current Viu SDK/framework
set, creates an isolated external-consumer restore boundary, publishes trimmed Browser and
hydration applications, generates hydration markup through the packaged
`ServerRenderAdaptor<TContext>`, installs the selected Playwright browsers, serves both published
`wwwroot` trees, and runs the same scenarios in Chromium, Firefox, and WebKit.

Scenarios cover clean boot, router navigation, click and input dispatch, a `v-model` round trip,
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
and AOT budget lanes.
