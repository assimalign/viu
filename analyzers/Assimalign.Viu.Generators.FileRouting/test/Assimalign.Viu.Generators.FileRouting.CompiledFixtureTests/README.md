# File-routing compiled fixtures

This project compiles the checked-in `.viu` and `.vue` files through both generators during the
ordinary project build. It uses the shared in-repository opt-in imports, so the same shipped
properties and additional-file graph drive this suite and SDK consumers.

The tests resolve every page through `GeneratedViuFileRoutes.Create()` and the real memory router,
then resolve the resulting references against `GeneratedViuComponents.Register(...)`. Dynamic
filenames deliberately exercise the syntax compiler's sanitized registration names. A component
outside `Pages/` is registered but is absent from the route table.
`OverridePage.viu` supplies a multiline `@route` block and `VuePage.vue` supplies a `<route>` custom
block, proving both containers coexist with the syntax generator while overriding paths and names.

The sibling layout `Pages/Blog.viu` compiles beside the `Pages/Blog/` directory
([V01.01.06.16], #363). Its `BlogHome.viu` page declares an empty path override, preserving unique
component names alongside root `Index.viu` while pinning the generated `blog-index` default-child
name. `Archive.viu` is the static child and `[Slug].viu` is the parameterized child.

The DOM-free rendering tests use `GeneratedViuFileRoutes.Create()` throughout. They resolve
`/blog`, `/blog/archive`, and `/blog/:slug` through the parent and render its literal
`<RouterView :depth="1" />` outlet. They also verify forwarding to the compiled child's
`[Parameter("slug")]`, parent retention across child navigation, and parameter-only child retention
(`[RTR-1]`, `[RTR-4]`, `[CMP-26]`).

The layout writes the literal outlet. Since [V01.01.08.03.02] (#359) the syntax checker reads
RouterView's hand-authored registration as an opaque contract, so no VIU1401 is reported and
nothing is suppressed; the factory registers the name `RouterView` because compiled component tags
resolve by registered name. Physical component-CSS bundling is disabled because this plain test
SDK has no static web asset pipeline or styles.
