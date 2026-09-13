# File routing diagnostics

The independently delivered `Assimalign.Viu.FileRouting` add-on carries this incremental generator
under `analyzers/dotnet/cs`. Its identifiers start at `VIU2001`, separate from the component syntax
generator's `VIU1xxx` range. These rules validate the add-on's authoring conventions; ordinary route
record semantics remain specified by [Routing](../../../docs/SPECIFICATION.md#12-routing).

| Identifier | Severity | Meaning and correction |
| --- | --- | --- |
| VIU2001 | Error | Two pages produce the same component registration identity. `ComponentFactory.Register` would throw at startup. Rename one page file; route `path` or `name` overrides cannot change component identity. This also detects distinct base names that sanitize to the same class name, such as `Foo-Bar` and `Foo_Bar`. |
| VIU2002 | Error | Two records produce the same resolved path. Rename a page/folder or override one route path. One layout and its single direct empty-path default child may intentionally share the path; two default children may not. |
| VIU2003 | Error | Two records produce the same route name, whether derived or explicitly overridden. Choose a unique `name` override or change the route. A default child derives the resolved name plus `-index`; its layout retains the ordinary derived name. |
| VIU2004 | Error | A `:name(.*)*` catch-all is followed by another effective route segment. Move the catch-all to the end, including across a layout/child boundary. An `Index` file adds no segment. |
| VIU2005 | Error | A file or folder segment is invalid. Use ASCII letters/digits with single hyphen or underscore separators, or `[Name]`, `[[Name]]`, `[...Name]`. Parameter names start with an ASCII letter and otherwise contain ASCII letters, digits, or underscores. |
| VIU2006 | Error | A route block/container is malformed or a path override is outside the supported grammar. Use one block, no header options, unique `path`/`name` assignments, double quoted values, and a semicolon after every assignment. Only quote and backslash escapes are accepted. Names must be nonempty. Root records require absolute paths; child records also accept relative or empty paths. |
| VIU2007 | Error | The enabled generator lacks `ProjectDir`, or the pages directory is empty/absolute. Consume the add-on through the Viu SDK and set `ViuFileRoutingPagesDirectory` to a project-relative directory. |

Page diagnostics point to the offending additional file. Configuration diagnostics have no source
location. Any error produces an empty generated table alongside the diagnostics, avoiding a partial
runtime table during editor recovery. Disabled generation emits neither diagnostics nor source.

A nonexistent or empty pages directory is not an error and has no informational diagnostic: the
generator consumes the SDK's additional-file graph without probing the filesystem and emits an empty
table when no page files are selected. Merely loading the analyzer does not enable generation; the
package's shipped props explicitly supplies `ViuFileRoutingEnabled=true` for consumers.

## Route block grammar

The public `Assimalign.Viu.Syntax.SingleFileComponent` parsers slice the container and its custom
blocks. Canonical `.viu` follows `[SFC-3]`: the header and closing brace occupy their own lines and
begin at column zero. The abbreviated one-line `@route { ... }` form is not a valid `.viu` container.

```text
@route {
    path = "/custom/:id";
    name = "custom";
}
```

The `.vue` compatibility container uses `<route>path = "/custom/:id"; name = "custom";</route>`
through the same public custom-block model `[VUE-2]`. No template or script content is scanned for
route-like text. Empty route blocks preserve defaults. Comments, expressions, external block sources,
and arbitrary regular expressions are outside this first grammar. Supported parameter path segments
are `:name`, `:name?`, `:name*`, `:name+`, and `:name(.*)*`; each occupies a whole segment. Static
override segments use the same ASCII word/separator validation as filenames and retain their authored
case. See the [runtime add-on design](../../../libraries/Utilities/Assimalign.Viu.FileRouting/docs/DESIGN.md)
for complete naming, layout, and component-compiler limitations.
