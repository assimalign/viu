# Single-file-component hot-reload metadata contract

Work item **[V01.01.06.05]** (issue #61) defines the compiler-owned metadata that lets Viu distinguish
template, script, and style updates without reparsing generated C# at runtime. This document is the
standalone contract between `Assimalign.Viu.Generators.Syntax` and downstream development hosts such as
[V01.01.12.05]. It applies equally to canonical `.viu` sources and the documented tag-based `.vue`
compatibility format.

The contract rests on a three-way separation of what an edit actually invalidated. A template edit
changes only what a component renders; a script edit changes the component definition itself; a style
edit changes neither, because styles are compiled into a static web asset that can be replaced without
touching a mounted component. Tracking the three independently is what lets a host do the least work an
edit permits instead of reloading everything.

Viu deliberately remounts on both template and script edits when running on .NET 10 browser
WebAssembly: already transformed call sites there can continue targeting a pre-update generated method
body even after the metadata delta is accepted, so an in-place re-render would risk executing stale
code. Remounting trades component-local state for a guarantee that the code that runs is the code that
was just edited. Style-only updates remain mounted and preserve state.

## Enablement

The generator reads two compiler-visible MSBuild properties through `AnalyzerConfigOptions`:

- `Configuration`: metadata is emitted by default when its value is `Debug` (case-insensitive).
- `ViuEmitHotReloadMetadata`: when set, this explicit Boolean overrides the configuration default.
  `true` opts any configuration in; `false` opts Debug out.

`sdks/Assimalign.Viu.Sdk/Targets/Assimalign.Viu.Generators.Syntax.props` publishes both properties as
`CompilerVisibleProperty` items.
An ordinary Release or AOT build therefore emits no hot-reload interface, hash getters, property
implementations, assembly attribute, or forwarding handler. The generator does not wrap dormant members in
conditional code; it omits them from the generated source.

## Generated API

An enabled component registers its stable source identifier and three compiler-generated marker types
through the Core hot-reload registry from a generated module initializer:

```csharp
global::Assimalign.Viu.ComponentHotReload.Register(
    typeof(Counter),
    "viu-127e199bfed3754c",
    typeof(SingleFileComponentTemplateUpdateMarker),
    typeof(SingleFileComponentScriptUpdateMarker),
    typeof(SingleFileComponentStyleUpdateMarker));
```

Each marker owns one method whose body contains only its block hash:

```csharp
private static class SingleFileComponentTemplateUpdateMarker
{
    internal static void TrackContentRevision()
    {
        global::System.GC.KeepAlive("4c8f0f9b725f36de");
    }
}
```

Only the marker for an edited block receives a changed method body. The .NET metadata-update callback's
`updatedTypes` set can therefore classify the delta by type identity without invoking the patched body.
This avoids reflection, runtime activation, generated revision delegates, and stale-body hash reads.

The generator also emits compiler-owned `internal static string` getters named
`HotReloadComponentIdentifier`, `HotReloadTemplateContentHash`, `HotReloadScriptContentHash`, and
`HotReloadStyleContentHash` for deterministic tooling inspection. Marker methods repeat the literals
because changing a `const` or static-field initializer is an `ENC0011` rude edit. Generated
`ExtractedStyles` follows the same Debug-getter/Release-constant split so an inline style value can change
without a field-initializer rude edit.

## Consumer-assembly metadata-update handler

When the same gate is enabled, the generator emits exactly one handler source for the consuming assembly,
even when that project currently has no single-file-component inputs. It registers:

```csharp
[assembly: global::System.Reflection.Metadata.MetadataUpdateHandlerAttribute(
    typeof(global::Assimalign.Viu.Generated.SingleFileComponentHotReloadMetadataUpdateHandler))]
```

The internal generated type follows the
[`MetadataUpdateHandlerAttribute`](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.metadata.metadataupdatehandlerattribute?view=net-10.0)
method convention:

```csharp
public static void ClearCache(global::System.Type[]? updatedTypes)
{
}

public static void UpdateApplication(global::System.Type[]? updatedTypes)
{
    global::Assimalign.Viu.ComponentHotReload.ApplyUpdates(updatedTypes);
}
```

`ClearCache` is deliberately a no-op. Core's mounted-component registrations retain the generated marker
type identities. `UpdateApplication`, after the runtime installs the metadata delta, forwards the
runtime's affected-type set and Core matches it against those identities. A script marker is dominant, a
template marker remounts the component without raising the script-reset notification, and a style marker
does no managed component work. If the runtime supplies no affected-type set, Core conservatively remounts
registered components.

The handler source is intentionally identical across component edits. The .NET Hot Reload agent retains
the delegate it creates for a metadata-update handler; making that handler manufacture revision-specific
delegates can leave browser WebAssembly bound to pre-update transformed bodies. Viu instead keeps this
cached delegate stable and uses changed marker-type identities as data. The handler lives in the consumer
assembly so the consuming Debug assembly registers the callback; an ordinary Release-built framework
package cannot register on its behalf.

## Component identifier

`ComponentIdentifier` is `viu-` followed by 16 lower-case hexadecimal digits. Its input is the source path
relative to `ProjectDir`, with directory separators normalized to `/`. The checkout root and file content
are not inputs, so the identifier is stable across machines and across edits while the project-relative
path remains unchanged. A linked source outside `ProjectDir` falls back to its leaf file name to avoid
embedding a machine-specific absolute path.

The hexadecimal portion is a deterministic 64-bit FNV-1a hash over a length-framed `component` label and
the identity path. Strings are processed as UTF-16 code units in little-endian byte order. Renaming or
moving a component intentionally changes the identifier.

## Content hashes

Each content hash is 16 lower-case hexadecimal digits produced by the same 64-bit FNV-1a primitive. Inputs
are length-framed, so different block boundaries cannot collapse into the same concatenated text.

- The template hash covers the template block's role, name, options in authored order, and exact raw
  content. A missing template has a deterministic empty-template hash.
- The script hash covers both the ordinary and setup script slots in source order. Each slot's role is
  framed separately, followed by its name, options, and exact raw content. Adding or deleting either slot
  changes this hash.
- The style hash covers every style block in source order. Each block's name, options, and exact raw content
  participates. An option-only edit such as adding `scoped` or changing a module name therefore invalidates
  the style revision even when the CSS text is unchanged.

Container whitespace outside these blocks and custom-block content do not participate. Adding, deleting,
reordering, changing options on, or editing content within a participating block changes only its category
hash. The component identifier remains unchanged.

These are deterministic change detectors, not security or integrity signatures.

## Update granularity

Core classifies the runtime's changed marker types for each registered component:

| Changed markers | Required action |
| --- | --- |
| Template only | Remount affected instances in the post-flush phase so .NET 10 browser WebAssembly executes the updated generated render body; reset component-local state |
| Script | Remount affected instances in the post-flush phase while retaining the Browser document; reset component-local state |
| Style only | Replace the component stylesheet in one batched update; perform no component work |
| Template and style | Remount affected instances in the post-flush phase and replace the stylesheet |
| None | No operation |

A script change is dominant for component work, so Core queues one component-local `ScriptReset`
remount for each affected mounted instance. Browser does not upgrade that accepted update to a
full-page reload; the .NET watch host owns rebuild and restart when a rude edit is rejected by metadata
update. A simultaneous style change still requires the independent stylesheet replacement. Viu chooses
reliable updated-code execution over in-place state preservation for template edits: on the supported
.NET 10 browser-WASM runtime, preserving state would require re-entering a generated body that may
already be stale, and Viu has no reflection or dynamic-code path available to resolve the newer one.

The source generator owns both this metadata and the consumer-assembly forwarding handler. Core owns the
mounted-instance registry and update classification; the development-server transport is a downstream
consumer. Neither may change the identifier or hash scheme described here.

## Safety and Release boundary

Hashing happens at compile time with integer and string operations only. Per-component generated code
contains static getter bodies, explicit property getters, and three hash-dependent marker methods; the
one consumer-assembly source adds only the sanctioned .NET metadata-update attribute and two static
forwarding methods. Viu performs no reflection, runtime serialization, file access, dynamic code
generation, or per-rule JavaScript interop. Release/default absence is covered for both `.viu` and
`.vue`, as are Debug and explicit opt-in emission, handler registration, and independent block
invalidation.
