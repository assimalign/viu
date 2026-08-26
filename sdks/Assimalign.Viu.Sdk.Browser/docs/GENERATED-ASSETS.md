# Generated-asset hot reload

`Assimalign.Viu.Sdk.Browser` exposes a versioned MSBuild seam for build extensions whose generated
static assets must be regenerated during an active Browser development session. The seam is a
development-time build contract: it does not add a runtime API, a JavaScript transport, or an
application asset. The existing BrowserRefresh client's targeted `UpdateStaticFile` message remains
the browser boundary under both `dotnet watch` and Visual Studio.

This contract is version 1 and is specified by [V01.01.12.30.04], issue #355, under [PKG-4].
The Browser SDK advertises it with:

```xml
<ViuGeneratedAssetSeamVersion>1</ViuGeneratedAssetSeamVersion>
```

An independently shipped provider must guard registration on exact equality. An absent value, or a
value other than `1`, means the provider performs its ordinary build-time generation and does not
register for live regeneration. Providers must not guess compatibility or bind to `_Viu*` names.

## Registration

A version-1 provider declares a public registration target that runs before
`ViuCollectGeneratedAssets` and adds one `@(ViuGeneratedAsset)` item per output:

```xml
<Target Name="ExampleRegisterGeneratedAsset"
        BeforeTargets="ViuCollectGeneratedAssets"
        DependsOnTargets="ExampleResolveGeneratedAssetInputs"
        Condition="'$(ViuGeneratedAssetSeamVersion)' == '1'">
  <ItemGroup>
    <ViuGeneratedAsset Include="$(ExampleGeneratedAssetPath)">
      <WatchFiles>@(ExampleResolvedInput->'%(FullPath)', ';')</WatchFiles>
      <WatchRoots>$(MSBuildProjectDirectory)</WatchRoots>
      <WatchExtensions>.example;.html</WatchExtensions>
      <RegenerationTarget>ExampleGenerateAsset</RegenerationTarget>
      <DependencyManifestPath>$(ExampleDependencyManifestPath)</DependencyManifestPath>
      <StaticWebAssetPath>wwwroot/example.css</StaticWebAssetPath>
      <RemovalBehavior>PreserveEmpty</RemovalBehavior>
    </ViuGeneratedAsset>
  </ItemGroup>
</Target>
```

The item contract is:

| Field | Requirement |
| --- | --- |
| `Identity` | Required absolute path of the generated asset. The path may be absent when the watch session starts. |
| `WatchFiles` | Optional semicolon-separated absolute input paths. Missing files remain dependencies so later creation is observable. |
| `WatchRoots` | Optional semicolon-separated absolute roots observed recursively, including roots that do not exist yet. |
| `WatchExtensions` | Semicolon-separated extensions accepted below `WatchRoots` and dependency-manifest roots. Each value includes its leading period. Required when any root is declared. |
| `RegenerationTarget` | Required public MSBuild target. The worker batches every distinct declared target into one nested build. |
| `DependencyManifestPath` | Optional absolute path to the version-1 dependency manifest described below. The provider owns and updates the file. |
| `StaticWebAssetPath` | Required stable route beginning with `wwwroot/`. The Browser SDK copies it to the generated asset's `@(Watch)` item for .NET watch and gives the same route to a Visual Studio-shaped RunHost session; either host sends `UpdateStaticFile` for that route. |
| `RemovalBehavior` | Required `Delete` or `PreserveEmpty`. `PreserveEmpty` is the stylesheet-safe removal protocol described below. |

At least one exact file, root, or dependency manifest must describe the asset's inputs. Paths and
target names are provider-owned public values; consumers of the seam never reference a provider's
private target, property, or resolved-item name.

## Host discovery

An ordinary Debug build with generated-asset hot reload enabled materializes the collected
descriptors in a deterministic worker configuration below `obj/viu/css-hot-reload/`. The file is an
internal host-discovery artifact, not an additional provider contract: providers continue to declare
only `@(ViuGeneratedAsset)`. The SDK byte-compares the complete configuration and preserves its
timestamp when the descriptors are unchanged. Worker configuration and state paths are resolved
against `MSBuildProjectDirectory`, so a Visual Studio solution working directory cannot merge two
projects into one ownership scope. The build itself does not leave a worker running.

The `dotnet watch` design-time watch-list pass starts its worker through the established watch path.
Visual Studio instead gives the packaged RunHost its BrowserRefresh websocket endpoint and public
key. RunHost consumes the ordinary-build configuration, starts the worker only when no live
watch-owned worker exists, and bridges the SDK-injected page client through a loopback websocket to
Visual Studio. Worker-generated updates use the same stable `StaticWebAssetPath` to originate a
targeted `UpdateStaticFile` message on that local connection. RunHost also retains every configured or
reported `.css` route and sends its current update message to each new browser connection, so reloads,
reconnects, and multiple tabs converge even when regeneration completed with no connected client.
Non-CSS routes are not replayed on connection because the stock BrowserRefresh client interprets
their `UpdateStaticFile` message as a document reload rather than an idempotent asset refetch. The
project-scoped state file and named mutex keep both hosts mutually exclusive. Specified by
[V01.01.12.30.05], issue #357.

## Regeneration guarantees

The active Browser development host starts one worker for the project, merges every asset's watched
inputs, and applies one quiet period to a burst of file-system events. One nested MSBuild invocation
runs the distinct
`RegenerationTarget` values with project-reference builds disabled and with:

```xml
<ViuGeneratedAssetHotReload>true</ViuGeneratedAssetHotReload>
```

That property is a driver signal, not an application setting. A provider target must be callable
with the active configuration, target framework, and runtime identifier; regenerate its declared
Identity deterministically; compare final bytes before writing; and leave the timestamp unchanged
when the bytes are identical. A write that is later discovered to be identical cannot be retracted
from the watch transport, so post-write comparison is not sufficient.

`RemovalBehavior=Delete` means the provider deletes an obsolete output. It is suitable only when the
host's use of that asset does not require a final static-file update. `PreserveEmpty` means that,
during `ViuGeneratedAssetHotReload=true`, removal of an existing stylesheet replaces it with a
zero-byte file. This gives `UpdateStaticFile` one final file write that retires every browser rule.
A normal build or publish must remove that development-only empty asset. A provider whose
regeneration target can be skipped incrementally may record private cleanup state, as the component
stylesheet provider does; that state is an implementation detail rather than part of the seam.

The Browser SDK's own component stylesheet bundle registers through this contract with `.viu` and
`.vue` inputs, `ViuGenerateSingleFileComponentCss`, its stable `wwwroot/<PackageId>.viu.css` route,
and `PreserveEmpty`. Existing component style-swap, semantic no-op, and remount behavior therefore
exercise the same generic path as add-on assets.

## Dependency manifest

An asset whose effective graph can discover files or roots while regenerating writes a separate
persistent manifest. The UTF-8, line-oriented version-1 format is:

```text
viu-generated-asset-dependencies-v1
file:<base64 UTF-8 absolute path>
root:<base64 UTF-8 absolute path>
```

Records may appear more than once; the worker de-duplicates them with the platform's path comparer.
`file:` records observe exact files, including absent files whose later creation matters. `root:`
records are recursive and inherit the declaring asset's `WatchExtensions`. Unknown headers are not
version 1 and are ignored with a diagnostic event rather than guessed.

The manifest is distinct from an editor or language-service sidecar. It must be written
deterministically, byte-compared, and retained even when the generated asset is empty so a future
dependency change can recreate that asset. Provider outputs and manifests normally live below
`obj`; the worker excludes generated directories from direct recursive observation to prevent
self-triggering loops and reads declared dependency manifests explicitly instead.

## Compatibility and lifecycle

The contract is collected into the worker configuration by ordinary Debug builds and by the Debug
`dotnet watch` design-time watch-list build. An ordinary build alone only writes that discovery
artifact. RunHost starts the Visual Studio worker only when BrowserRefresh is present; a direct
`dotnet run` without that environment remains inert. Release builds and publish do not emit the
configuration or launch a worker, and hosts that do not advertise version 1 continue to use each
provider's ordinary build-time target. A breaking metadata, manifest, driver-property, or lifecycle
change requires a new seam version; exact-version provider guards keep incompatible SDK and add-on
versions inert.
