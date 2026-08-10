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

Open the address printed by the watch host if it does not open automatically. Keep that page
connected for the session. The SDK injects component stylesheet links during the build; no
development script or hand-authored stylesheet link is required.

The CSS worker starts only for the Debug, design-time watch-list build. It waits for the default
100-millisecond quiet period after the final file event, regenerates component and utility bundles
in one nested MSBuild invocation, and writes a bundle only when its bytes changed. Override the
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
| Utility entry, theme, reference, or standalone utility source | Regenerate the utility bundle and replace its linked stylesheet | Page, application, and mounted component state are retained |
| Template body accepted by metadata update | Remount only affected component instances in Viu's post-flush phase, then commit the buffered host operations | The document and other components remain mounted; affected component-local state resets |
| Utility class changed inside `<template>` | Regenerate utility CSS and take the template-update path because the authored template also changed | The affected component remounts and its local state resets |
| Script marker, declaring component type without a more specific marker, or missing updated-type set | Remount only affected component instances in Viu's post-flush phase while the applied managed delta remains loaded | The document and other components remain mounted; affected component-local state resets |
| Signature, property-surface, or another edit rejected by metadata update | The .NET watch host rebuilds/restarts and refreshes the browser | Application and component state reset |
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

The CSS transport is independent of managed component replacement. Consequently, changing a
utility candidate in template text is not a style-only edit: it changes both the utility stylesheet
and the compiled template. Put a pure visual change in `<style>` or a utility theme/source file when
mounted component-state preservation is required.

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

The worker and its state file are SDK build tools, not application assets. Ordinary Release builds
disable worker launch, the generator omits its metadata handler and marker members, and the worker's
`Watch/Assimalign.Viu.Sdk.CssHotReload.*` files are never copied into application publish output.
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
a `.vue`-only package consumer, requires its component and utility assets, and scans generated and
published code for absent Debug-only metadata; add `-Aot` to exercise the same boundary after AOT
compilation. The separate `scripts/Test-EndToEnd.ps1 -HotReload -Configuration Debug` lane starts
an isolated packaged consumer under `dotnet watch` and uses a connected Chromium page to prove
utility stylesheet replacement, document identity, mounted-state survival, template/script remount,
semantic no-op suppression, and final-rule removal. These opt-in modes remain separate from the
ordinary three-scenario browser matrix because the live lane mutates staged sources and owns a
long-lived watch process tree.
