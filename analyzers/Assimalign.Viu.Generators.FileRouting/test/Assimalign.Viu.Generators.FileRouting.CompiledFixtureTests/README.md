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

The DOM-free rendering test uses a separately named `Components/BlogLayout.viu` and an explicit
descriptor tree. It verifies the compiled layout's literal `<RouterView :depth="1" />` outlet,
forwarding to the compiled child's `[Parameter("slug")]`, parent retention, and parameter-only
child retention (`[RTR-4]`, `[CMP-26]`).

The layout writes the literal outlet. Since [V01.01.08.03.02] (#359) the syntax checker reads
RouterView's hand-authored registration as an opaque contract, so no VIU1401 is reported and
nothing is suppressed; the factory registers the name `RouterView` because compiled component tags
resolve by registered name. Physical component-CSS bundling is disabled because this plain test
SDK has no static web asset pipeline or styles.

The current syntax compiler derives namespaces from directories and classes from filenames.
Consequently, `Pages/Blog.viu` would declare the type `Pages.Blog`, while
`Pages/Blog/[Slug].viu` would declare the namespace `Pages.Blog`. That type/namespace collision is a
compiler limitation of the proposed sibling-layout convention; the real both-generator build reports
CS0101 on the generated `Pages.Blog.SingleFileComponent.g.cs`. This suite keeps that colliding pair
out of its passing build; generator tests cover the descriptor nesting separately. Changing syntax
compiler identity requires a separate decision and is outside this add-on's implementation scope.
