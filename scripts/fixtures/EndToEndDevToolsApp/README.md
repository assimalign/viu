# Packaged DevTools browser fixture

This Browser SDK consumer is both the inspected sample and the thin packaged client host for
[V01.01.10.03](https://github.com/assimalign/viu/issues/83). It lives in `scripts/fixtures` because
the repository Packaging lane stages isolated package consumers; it is deliberately excluded from
the ordinary solution's project-reference build.

The default document mounts `InspectedCounter`, enables the runtime postMessage session and
registers a sample inventory inspector. Its iframe loads the same published application with
`?panel`, which mounts the package's `.viu` panel components with a client session and no runtime
inspection session. Independent WebAssembly runtimes prevent the panel's own rendering from
generating inspected telemetry. The embedding host relays only same-origin server and readiness
frames from its own window to the panel; the client sends requests to the parent window. Every
application/client value crosses that boundary as the versioned JSON protocol [DVT-14].

The `?panel` URL is also the thin panel-host entry point. Its default postMessage connection
requires a same-origin embedding host or extension bridge. A separately hosted panel can select
the WebSocket transport with `?panel&endpoint=wss%3A%2F%2Fexample.test%2Finspection`; the endpoint
must be an absolute `ws` or `wss` URI. The supplied endpoint must relay the inspected runtime's
complete protocol batches. The fixture does not provide a relay service.

The host registers the package's JavaScript and CSS as stable `_content` routes and links the
stylesheet in its entry document. Its application entry module remains fingerprinted. This
fixture-specific asset registration keeps the client package independent of a Browser SDK target.

Run `pwsh scripts/Test-EndToEnd.ps1 -DevTools` for a trimmed package publish and Chromium
tree, snapshot, lazy expansion, edit, timeline correlation, and generic-inspector checks.
Use `-DevTools -PublishOnly -PublishDirectory _out/devtools-publish` to retain the trimmed host
without launching Chromium. Both paths use warning-as-error publication. The script explicitly
packs the ecosystem client library after the ordinary package inventory.
