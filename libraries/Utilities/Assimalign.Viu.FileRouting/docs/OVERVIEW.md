# Assimalign.Viu.FileRouting

`Assimalign.Viu.FileRouting` is a standalone Viu Utilities add-on. It translates the folder structure
of a Viu SDK project's page components into an ordinary, eager route table. It adds no core routing
semantics and stays outside every Viu SDK and shared-framework surface. Router continues to own
path joining, matching, ranking, history, navigation, and view selection (`[RTR-1]` through `[RTR-4]`).

The package contains a small host-free runtime library, the incremental
`Assimalign.Viu.Generators.FileRouting` analyzer and its parser closure under `analyzers/dotnet/cs`,
and `build/Assimalign.Viu.FileRouting.props`. A single package reference from either the base or
Browser Viu SDK enables generation. The Viu SDK supplies the existing `.viu`/`.vue` AdditionalFiles
graph and syntax generator; FileRouting filters that graph rather than discovering files itself.

The generator emits `GeneratedViuFileRoutes.Create()` in the consumer's root namespace. Its one
source file declares a tree of `FileRouteDescriptor` values and passes that tree to
`FileRoutes.Create`. The runtime converts descriptors into `RouteRecord` instances containing
`ComponentNode(ComponentReference.ForName(...))` requests. These requests use explicit generated
component registrations (`[CMP-6]`, `[CMP-7]`); they do not activate types from strings.

Pages with route parameters forward the resolved values through
`RouteComponentArguments.FromParameters()` (`[RTR-4]`). Component `[Parameter]` declarations own
the accepted argument names (`[CMP-26]`). Layout nesting is expressed as ordinary child records and
the author's explicit `RouterView` depths, with no hidden component-tree dependency channel.

The current component compiler produces a C# type/namespace collision for a sibling-file layout
and its same-named folder. That existing compiler boundary prevents those physical layout trees
from compiling through both generators; use flat pages and manually composed nested descriptors
until the separate namespace-generation follow-up. The route generator and runtime already support
the nested table shape. [DESIGN.md](DESIGN.md#current-component-compiler-layout-limitation) records
the scope and evidence.

Descriptor construction snapshots child lists, and conversion creates independent record trees.
There is no runtime reflection, code generation, assembly scanning, JSON, browser dependency, or
Core dependency. Every generated route is eager; the package makes no lazy assembly promise
(`[RTR-7]`, `[RTR-8]`, `[RTR-11]`).

The [README](../README.md) contains the consumer quickstart. [DESIGN.md](DESIGN.md) records the exact
folder conventions, their diagnostic boundaries, and the deliberately limited override grammar.
