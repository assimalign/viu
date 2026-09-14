# Static prerender packaged consumer

`scripts/Test-EndToEnd.ps1` stages this Browser SDK application with `EndToEndPrerenderHost` and
`EndToEndPrerenderShared` behind an isolated package feed. It publishes with
`ViuStaticPrerender=true`; the base SDK then builds the designated server executable and runs
`prerender --host-page <published-wwwroot/index.html> --output <published-wwwroot> --routes routes.txt`.
The host uses explicit registrations and a fresh application, memory router, request scope, and
state registry for each route. This fixture covers [V01.01.07.05], #68.

Both `/` and `/guide/intro` preserve the published fingerprinted WebAssembly bootstrap. The host page
authors `<base href="/">` so directly loaded nested pages resolve the same assets. Browser composition
uses web history and restores the source-generated state payload before component setup. The nested
route resolves `slug=intro` through `RouterView`; unmatched routes abort in the navigation guard.

The harness visits each emitted document, verifies server state and node identity, observes zero
hydration DOM mutations except consumption of `script[data-viu-state]`, then clicks the adopted
button and checks its reactive update. The runtime probe lives only in this test fixture.
