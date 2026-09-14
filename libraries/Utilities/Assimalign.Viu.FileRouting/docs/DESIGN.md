# Assimalign.Viu.FileRouting design

## Authority and dependency boundary

This is a non-normative Utilities add-on that produces ordinary `RouteRecord` values. Its
conventions are local product decisions pinned by the add-on tests; the routing and component
contracts remain those of [`SPECIFICATION.md`](../../../../docs/SPECIFICATION.md), especially
`[RTR-1]`, `[RTR-4]`, `[RTR-7]`, `[CMP-6]`, and `[CMP-7]`. The add-on is tracked as work item
[V01.01.08.08] (#361); this implementation does not invent a specification clause.

The runtime directly references only `Assimalign.Viu.Router` and `Assimalign.Viu.Components`.
The generator is a netstandard2.0 Roslyn incremental component, referencing the public Syntax and
Syntax.SingleFileComponent parser libraries. The runtime never references those parser assemblies;
their minimal closure ships beside the analyzer. Neither project depends on Core or Browser.

## Discovery and reproducibility

The pages root defaults to `Pages` relative to `ProjectDir` and can be changed with
`ViuFileRoutingPagesDirectory`. Only `.viu` and `.vue` AdditionalFiles under this root are route
candidates. The Viu SDK owns those inputs, RootNamespace, ProjectDir, and component generation.
FileRouting performs no directory enumeration or filesystem existence check. An absent or empty
root produces an empty table without a diagnostic. Components elsewhere remain outside this
add-on's discovery and duplicate-component diagnostics.

`ViuFileRoutingEnabled=false` suppresses the generated file entirely. A normal enabled project gets
one deterministic `Viu.FileRoutes.g.cs` containing a public static `GeneratedViuFileRoutes` in its
root namespace and a public `Create()` returning `IReadOnlyList<RouteRecord>`.

Input identity includes AdditionalText path and text, the two add-on properties, and the root
namespace/project-directory context needed to resolve them. The pipeline does not consume the C#
compilation, so an unrelated C# edit cannot trigger route source emission. Topology, collision
comparison, and output ordering use ordinal case-sensitive names; extension recognition and the
special `Index` basename are case-insensitive. Table records remain in stable ordinal order.
The matcher, rather than table order, chooses specificity under `[RTR-1]`.

## Static and dynamic segments

Static file or directory names contain ASCII letters and digits, optionally separated by single
hyphens or underscores. Leading, trailing, or consecutive separators and other punctuation are
invalid. Both separators produce `-`. Letter case is lowered invariantly. A word boundary is
inserted before an uppercase character when its preceding character is lowercase or a digit, or
when that uppercase character ends an uppercase run and is followed by lowercase. Digits remain
attached to the preceding word. For example:

| Input | Segment |
|---|---|
| `QuickStart` | `quick-start` |
| `Blog` | `blog` |
| `XMLParser` | `xml-parser` |
| `HTTP2Server` | `http2-server` |
| `Version2` | `version2` |
| `V2Page` | `v2-page` |
| `getting_started` | `getting-started` |

The extension is omitted. `Index.viu` at the pages root maps to `/`. An Index page inside an
ordinary folder maps to that folder's path. Its empty segment becomes an empty relative child
when there is a containing layout.

Dynamic segments may appear in files or directories:

| Input | Pattern | Meaning |
|---|---|---|
| `[Slug]` | `:slug` | Required scalar parameter |
| `[[Slug]]` | `:slug?` | Optional scalar parameter |
| `[...Rest]` | `:rest(.*)*` | Zero or more remaining path segments |

A parameter name starts with an ASCII letter and contains only ASCII letters, digits, and
underscores. It is lowercased without word-boundary insertion (`[BlogSlug]` becomes `:blogslug`),
because Router's parameter grammar does not accept hyphens. Catch-all parameters must be last in
the effective route path. A catch-all layout cannot have a nonempty descendant path.

Any parameters in the effective path, including inherited parent parameters or override parameters,
set `ForwardParameters=true`. The runtime assigns `RouteComponentArguments.FromParameters()`:
scalar values remain strings and repeatable values are joined with `/`. Pages without parameters
have no arguments resolver. The page declares names explicitly through `[Parameter("slug")]`, for
example, under `[CMP-26]`; the add-on does not infer, rename, or generate parameter declarations.

## Layouts, names, and explicit view depth

A file `Blog.viu` beside a `Blog/` directory is the layout for that directory. Its record is `/blog`
and its descendants become relative children. A folder without a sibling layout contributes its
segments to a flat path; beneath an existing layout those segments still form a relative child
path. Multiple layouts nest recursively, using the nearest layout as parent.

An Index page immediately under a layout has an empty child path. The matcher resolves the default
child ahead of its parent while retaining both in the matched chain (`[RTR-1]`). This intentional
parent/default-child pair is the only allowed duplicate effective path.

Route names derive from effective path segments joined with `-`, with parameter syntax removed:
`/` gives `index`, `/blog/:slug` gives `blog-slug`. A default child appends `-index`, so `Blog.viu`
is `blog` and `Blog/Index.viu` is `blog-index`. This explicit exception prevents the default-child
pattern from inevitably producing duplicate names. Other duplicate names and effective paths are
errors. Overrides can assign a different unique name.

`RouterView` selects an explicit index into the matched chain (`[RTR-4]`). The application's outlet
uses depth 0. A parent layout at matched depth 0 renders `<RouterView :depth="1"/>`; a nested
layout at depth 1 renders `<RouterView :depth="2"/>`. The generator emits the correct required
child-outlet depth in comments alongside layout descriptors. It does not edit author templates,
create implicit outlets, or propagate ambient depth.

## Component registration identity

The syntax compiler registers component names derived from file basenames, independent of their
directory namespaces (`[SFC-CG-5]`, `[CMP-6]`). Punctuation becomes `_`, leading digits acquire an
underscore, and an escaped C# keyword's registration name omits the escape. For example `[Slug].viu`
registers `_Slug_`, not `Slug` or the bracketed filename. The route generator emits exactly that
registration identity and diagnoses duplicate basenames and sanitized registration collisions
among pages. It does not modify the core syntax generator.

Duplicate names would cause `ComponentFactory.Register` to throw at startup even though separate
namespaces let the generated classes compile. Rename one file to fix this. In particular, two
`Index.viu` pages cannot coexist in one assembly; use a uniquely named page with a route override
to preserve its URL. Registration collisions with non-page components, across separately compiled
assemblies, or with handwritten registrations remain the application's responsibility.

The add-on rejects same-stem `.viu`/`.vue` peer inputs as duplicate pages too. The syntax compiler
normally shadows the `.vue` peer under `[VUE-7]`, so that specific pair would not itself produce a
duplicate registration at startup. Requiring one page input makes FileRouting's discovery
unambiguous; it is a stricter add-on convention, not a change to container or parser semantics.

## Compiled sibling layouts

The compiled fixture includes `Pages/Blog.viu` alongside `Pages/Blog/Archive.viu` and
`Pages/Blog/[Slug].viu`. The component compiler disambiguates the layout's generated C# identity
under `[SFC-CG-10]` and [V01.01.06.16] (#363), while preserving its `Blog` registration name.
FileRouting continues to emit the same names and ordinary relative child descriptors.

`Pages/Blog/BlogHome.viu` declares an empty path override, so the generated table also exercises
the `blog-index` default child while keeping the root `Index.viu` registration unique. Tests
resolve `/blog`, `/blog/archive`, and `/blog/:slug` through the generated parent record and render
the nested outlet through the Testing host, including parent and parameter-only child retention.

The layout writes the literal outlet `<RouterView :depth="1"/>`. Since [V01.01.08.03.02]
(#359), its hand-authored registration is an opaque parameter contract for the syntax checker.
The compiled fixture registers the name `RouterView` with the ordinary RouterView registration's
contract and activator, because compiled component tags resolve by registered name. This setup
compiles without suppressing diagnostics.

## Optional route block

The public SingleFileComponent parser cleanly exposes custom blocks for both containers. A `.viu`
page may declare:

```text
@route {
    path = "/custom/:id";
    name = "custom";
}
```

The closing brace obeys the ordinary `[SFC-4]` container rule: column zero on its own later line.
The `.vue` counterpart is `<route>path = "/custom/:id"; name = "custom";</route>`.
At most one route block is accepted.

The grammar is zero or more `path` or `name` assignments, each property at most once. Values are
double-quoted strings followed by a mandatory semicolon. Whitespace is allowed; the only escapes
are `\\` and `\"`. Comments, expressions, unknown properties, and nested objects are rejected.
An empty block keeps all inferred conventions.

Root paths must be absolute. Nested paths may be absolute, relative, or empty; parent overrides
flow into relative descendants. Overrides preserve authored static segments and accept only
whole-segment `:name`, `:name?`, `:name*`, `:name+`, and `:name(.*)*` parameter patterns. Arbitrary
regular expressions and mixed static/parameter segments are intentionally outside this tiny
grammar, although handwritten Router records can use Router's larger grammar. Catch-all position,
duplicate paths, and duplicate names are validated after overrides. The final effective path drives
parameter forwarding and inferred route naming.

## Packaging, runtime shape, and AOT

The library's `ViuAnalyzerReference` packs the generator and its public parser closure under
`analyzers/dotnet/cs`, using the analyzer delivery convention documented by `[PKG-3]`. The library
and generator remain outside every framework manifest and SDK surface. Consumers install one
ordinary FileRouting package reference into a base or Browser Viu SDK project.

The package owns `build/Assimalign.Viu.FileRouting.props`, which defaults and exposes only the two
add-on properties. **Recorded deviation from the repository's two homes rule:** an independently
installed add-on owns its consumer wiring beside its package, following UtilityCss.Build; the Viu
SDK does not acquire an add-on dependency. The library also records a narrow deviation from the
central README rule to ship its own quickstart. In-repo projects set `ViuUseFileRouting=true` and
import this same props file through `Build.References.Analyzers.targets`, preventing packaged and
dogfooded paths from drifting. The add-on never duplicates the SDK's AdditionalFiles graph.

The generated file uses globally qualified names and a declarative `FileRouteDescriptor` tree;
it introduces no underscore-prefixed author names or file-level static imports. A descriptor
snapshots its children and validates non-null path plus nonempty route and component names.
`FileRoutes.Create` builds a fresh record tree each time, retaining order and declared relative
paths. It defers path validation/ranking to Router when called with hand-authored descriptors.

Every record eagerly holds a `ComponentNode` with an explicit registered-name reference.
Descriptors do not contain runtime type names or activation callbacks, and the runtime never
loads assemblies, probes constructors, emits code, scans the filesystem, or serializes JSON.
The library targets the central runtime framework alias with `IsAotCompatible=true`. There is no
lazy factory: `[RTR-8]` remains Router's separate capability, and `[RTR-11]` forbids promising a
supported plain-WebAssembly lazy loader that does not exist.

## Diagnostics, tests, and non-goals

The generator's own `VIU2001` range covers duplicate component identities, duplicate route paths
and names, invalid segment names, catch-all placement, malformed overrides, and enabled generation
with missing or invalid project-directory configuration. See its
[diagnostic catalog](../../../../analyzers/Assimalign.Viu.Generators.FileRouting/docs/DIAGNOSTICS.md)
for precise identifiers and fixes. No diagnostic is emitted solely because there are no pages.

Generator tests pin discovery, every mapping and diagnostic, overrides, source determinism, and
incremental caching. Runtime tests exercise descriptor conversion, snapshots, validation failures,
argument forwarding, default/nested paths, specificity ranking, and real memory-router navigation.
The compiled fixture consumes both real generators and checks generated component registration.

Non-goals are changing Router, Syntax, or SDK semantics; runtime discovery; component activation;
lazy assemblies; automatic layout outlets or depth propagation; parameter declaration synthesis;
route guards, aliases, redirects, named views, or metadata in route blocks; and reflection-based
registration. Such features need separate designed work rather than expanding this grammar by
accident.
