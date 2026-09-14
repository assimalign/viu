# Viu documentation

Viu is a standalone C#/.NET user-interface framework for the browser. It combines explicit reactive
state, immutable virtual-node descriptions, and compiler-informed rendering through .NET WebAssembly.
Roslyn source generators compile components and templates at build time, keeping the runtime safe
for trimming and ahead-of-time compilation. Both `.viu` components and compatible `.vue` containers
are supported.

**Package version: {{ViuVersion}}.** This documentation describes the behavior implemented in this
preview: the component and application lifetimes, reactive state, browser rendering and hydration,
routing, server rendering, and the base SDK's static prerendering entry point. The
[specification](../SPECIFICATION.md) records those shipped contracts and their
[current limits](../SPECIFICATION.md#18-non-goals-and-current-limits). Planning documents linked from
the reader guides may also describe future work; they are not release guarantees.

## Start here

Follow [Getting started](../guide/getting-started.md) to create, run, and publish a Browser application
with the packaged Viu SDK. Browse the external
[viu-examples sample gallery](https://github.com/assimalign/viu-examples) for packaged consumer examples.

## Guides

[Developer examples](../DEVELOPER-EXAMPLES.md) explains component setup, reactive state, application
composition, and library consumption. The Guides navigation includes every reader guide
under `docs/guide/` as it is added.

## Libraries

Choose the area that owns the behavior you need:

- Runtime: [Components](../../libraries/Runtime/Assimalign.Viu.Components/docs/OVERVIEW.md),
  [Core](../../libraries/Runtime/Assimalign.Viu.Core/docs/OVERVIEW.md), and
  [Reactivity](../../libraries/Runtime/Assimalign.Viu.Reactivity/docs/OVERVIEW.md).
- Browser: [DOM host](../../libraries/Browser/Assimalign.Viu.Browser/docs/OVERVIEW.md) and
  [browser routing](../../libraries/Browser/Assimalign.Viu.Browser.Router/docs/OVERVIEW.md).
- [Router](../../libraries/Router/Assimalign.Viu.Router/docs/OVERVIEW.md): matching, history, and navigation.
- [State](../../libraries/Runtime/Assimalign.Viu.State/docs/OVERVIEW.md): stores and registry lifetimes.
- [ServerRenderer](../../libraries/ServerRenderer/Assimalign.Viu.ServerRenderer/docs/OVERVIEW.md):
  HTML serialization and static prerendering.
- DevTools: [Testing](../../libraries/DevTools/Assimalign.Viu.Testing/docs/OVERVIEW.md), the DOM-free test host.
- [Syntax](../../libraries/Syntax/Assimalign.Viu.Syntax/docs/OVERVIEW.md): public parsers for templates,
  CSS, HTML, and single-file components.
- Utilities: [FileRouting](../../libraries/Utilities/Assimalign.Viu.FileRouting/docs/OVERVIEW.md) and
  [UtilityCss](../../libraries/Utilities/Assimalign.Viu.UtilityCss/docs/OVERVIEW.md), standalone add-ons.
- SDKs: [component-library and Browser SDKs](../../sdks/README.md) and
  [static prerendering](../../sdks/Assimalign.Viu.Sdk/docs/STATIC-PRERENDER.md).

The Libraries navigation includes every library overview and SDK reader document. Linked design
documents explain lifetime, host, and build constraints beside the corresponding overview.

## Specification

[The Viu specification](../SPECIFICATION.md) defines Viu's own semantics. Clause citations in API
documentation link directly to stable specification anchors. UtilityCss is a standalone add-on;
its compatibility target does not define Viu core behavior.

## API reference

Use the API reference navigation to browse public types and members, starting with the
[Core namespace](xref:Assimalign.Viu). The reference includes runtime libraries, public Syntax parsers,
and standalone Utilities APIs; every page carries the same package-version label.
