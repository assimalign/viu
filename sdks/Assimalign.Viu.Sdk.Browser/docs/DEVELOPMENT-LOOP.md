# Browser development loop

The Browser SDK owns Viu's inner development loop ([V01.01.12.05], issue #94). It composes the
.NET watch host, the single-file-component generator, Core's hidden component-update ABI, and the
Browser renderer. No runtime reflection, assembly scanning, or runtime template compilation is
used. Generated marker types cross the compiler/runtime boundary specified by [SFC-CG-2] and
[SFC-CG-4].

## Start a session

Use a Debug Browser application whose project starts with:

```xml
<Project Sdk="Assimalign.Viu.Sdk.Browser">
```

From the application directory, run:

```powershell
dotnet watch
```

The `viu-app` template supplies one active launch profile with `launchBrowser=true` and a pinned
`applicationUrl`. Existing applications must add the same shape themselves; the SDK deliberately
does not choose or force a port for an existing project. Keep the URL fixed for the whole watch
session. `ASPNETCORE_URLS` also pins WasmAppHost, but the selected launch profile must still enable
browser launch, and `DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER` must remain unset, for `dotnet watch` to
install its restart-reload observer.

Open the printed address if it does not open automatically and keep that page connected. On .NET
SDK 10.0.302, the observer recognizes the literal `Now listening on:` while WasmAppHost 10.0.11
prints `App url:` after binding. The Browser SDK therefore runs WasmAppHost through its packaged
run host, which forwards every original standard-output and standard-error line and adds the
recognized form after each valid `App url:` line. After a rude-edit rebuild, that readiness event
makes `dotnet watch` send `Reload` to the already-connected browser. The added line is harmless
under ordinary `dotnet run`.
Set `ViuBrowserRunHostReadinessEnabled=false` only when a custom run host supplies an equivalent
readiness contract.

A fixed origin and the readiness mirror solve different halves of the restart path. Without a
pinned URL, WasmAppHost selects a new random port after the restart, so the browser's old origin is
dead and cannot be followed. The SDK injects component stylesheet links during the build; no
development script or hand-authored stylesheet link is required.

The CSS worker starts only for the Debug, design-time watch-list build. It waits for the default
100-millisecond quiet period after the final file event, regenerates the component bundle in one
nested MSBuild invocation, and writes it only when its bytes changed. Override the
quiet period with `ViuCssHotReloadDebounceMilliseconds`, or disable this part of the loop with
`ViuCssHotReloadEnabled=false`.

Visual Studio's ordinary Hot Reload command does not invoke this watch-list contract. Launch the
project through `dotnet watch` when stylesheet regeneration is required.

## Update decisions

The generator gives every component a stable identifier and template, script, and style marker
types. When the runtime reports more than one type, Viu applies this precedence:

1. script marker;
2. template marker;
3. style marker;
4. declaring component type as the conservative fallback.

A missing updated-type set is also conservative. A genuinely unrelated updated type causes no Viu
component work; the .NET watch host remains responsible for that update.

| Edit | Browser action | State boundary |
| --- | --- | --- |
| Content or options inside a component `<style>` block | Regenerate the component bundle and replace its linked stylesheet; Core performs no component work | Page, application, and mounted component state are retained |
| Template body accepted by metadata update | Remount only affected component instances in Viu's post-flush phase, then commit the buffered host operations | The document and other components remain mounted; affected component-local state resets |
| Script marker, declaring component type without a more specific marker, or missing updated-type set | Remount only affected component instances in Viu's post-flush phase while the applied managed delta remains loaded | The document and other components remain mounted; affected component-local state resets |
| Signature, property-surface, or another edit rejected by metadata update | The .NET watch host rebuilds/restarts; the packaged run host reports readiness and the watch refresh server reloads the connected browser on the pinned origin | The document, application, and component state reset; without a fixed application URL the browser remains on the abandoned origin |
| CSS edit whose deterministic output is byte-identical | Do not rewrite the bundle and send no stylesheet update | All state is retained |
| Updated type unrelated to registered component metadata | Viu performs no component action | Determined by the .NET update that owns that type |

Template and supported script updates deliberately do not preserve the affected component instance
on .NET 10 browser WebAssembly. Already transformed call sites can otherwise continue targeting a
stale generated method body, while a document reload after an accepted metadata update would discard
the live delta and restart from stale on-disk assemblies. A post-flush remount guarantees that the
newly compiled body runs without interleaving with an active patch and retains the current document.
State-preserving in-place template rerendering requires the later per-block runtime refinement tracked
outside [V01.01.06.05]. That work item supplies the stable per-block classification metadata; it does
not weaken the .NET 10 browser-WASM stale-call-site constraint or promise in-place template rerendering.

The component-CSS transport is independent of managed component replacement. A pure `<style>` edit
can therefore replace the stylesheet without remounting the component, while a template edit still
takes the managed template-update path.

## Expected latency

The 100-millisecond CSS quiet period is the only fixed delay. On a warm local Debug session, use
these operational expectations rather than hard guarantees:

| Path | Typical save-to-visible interval |
| --- | --- |
| Stylesheet link replacement | approximately 0.2-1 second |
| Accepted template update and affected-component remount | approximately 0.5-2 seconds |
| Rebuild, restart, and full document reload | approximately 1-5 seconds |

Project size, machine speed, WebAssembly workload state, antivirus scanning, and browser startup
can move those ranges. The watch-host build messages are the authoritative timing evidence for a
particular session. A semantic CSS no-op has no visible-update latency because it writes and sends
nothing.

## Release boundary

The CSS worker, its state file, and the readiness run host are SDK development tools, not application
assets. Ordinary Release builds disable worker launch, the generator omits its metadata handler and
marker members, and neither `Watch/Assimalign.Viu.Sdk.CssHotReload.*` nor
`RunHost/Assimalign.Viu.Sdk.Browser.RunHost.*` is copied into application publish output.
The platform browser-refresh channel is supplied only by the active development host. Release and
AOT output are checked through `scripts/Measure-PublishBudget.ps1` against the reviewed manifest in
`scripts/budgets/PublishBudgets.json`.

`ViuEmitHotReloadMetadata=true` is an explicit diagnostic opt-in that overrides the ordinary
configuration gate. Do not set it for a production publish.

## Verification status

Unit and integration tests pin marker classification, post-flush remount ordering, watch-item
registration, deterministic no-op writes, final-rule tombstones, worker lifetime, and the Release
publish budget. The opt-in
`scripts/Test-EndToEnd.ps1 -PackagedVuePublish -PublishOnly -Configuration Release` lane publishes
a `.vue`-only package consumer, requires its component-CSS asset, and scans generated and
published code for absent Debug-only metadata; add `-Aot` to exercise the same boundary after AOT
compilation. The separate `scripts/Test-EndToEnd.ps1 -HotReload -Configuration Debug` lane starts
an isolated packaged consumer under `dotnet watch` and uses a connected Chromium page to prove a
structural rude edit restarts on the same port and automatically reloads to new content without
manual navigation, component stylesheet replacement, accepted-update document identity,
mounted-state survival, template/script remount, and semantic no-op suppression. These opt-in modes
remain separate from the ordinary three-scenario browser matrix because the live lane mutates staged
sources and owns a long-lived watch process tree.
