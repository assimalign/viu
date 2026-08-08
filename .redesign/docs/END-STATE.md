# Adopted runtime model

The staged system separates immutable descriptions, authored behavior, and mounted bookkeeping.

| Lifetime | Public concept | Owner |
|---|---|---|
| Immutable render description | `VirtualNode` and sealed variants | Components |
| Static identity and input/output declaration | `ComponentReference`, `ComponentContract`, `ComponentRegistration` | Components |
| One authored instance per mount | `IComponent`, `ComponentContext`, `ComponentRenderer` | Components |
| Reconciliation, scopes, scheduling, host ranges | internal mounted engine and public views/leases | Core |

`ComponentRenderFrame` owns each mount's cache and block assembly. A compiled block supplies a
`RenderPlan`; `DynamicChildren == null` requests ordinary diffing, an empty list skips all child
visits, and a populated list visits only direct dynamic descendants (`[RND-BLOCK-1]` through
`[RND-BLOCK-4]`). Code-first output defaults to `RenderPlan.None` (`[CMP-34]`).

Conventions do not expand `ComponentContext`. State resolves its registry through `Services` and
then its ambient registry (`[STA-4]`, `[CMP-33]`). Router components resolve `Router` through
`Services`; guards receive the same public context (`[RTR-5]`, `[RTR-9]`).

Core exposes operation-shaped seams. ServerRenderer borrows a component-render lease, serializes
its tree, then disposes it. Testing consumes stable mounted views and the production renderer over
an in-memory host. Browser supplies DOM operations and one hydration snapshot. None requires
access to another library's internals.

Router keeps matching and history policy together. `CurrentRoute` is reactive, guards run in the
specified order, redirects restart resolution, `RouterView` advances an explicit outlet depth,
and `RouterLink` renders through the host-neutral tree (`[RTR-1]` through `[RTR-9]`). Browser.Router
only binds document clicks and Router readiness/cleanup to Browser application lifetime.

All activation and dispatch are registration- or delegate-based. Roslyn generation is the only
metaprogramming path; runtime reflection activation and dynamic code generation are absent.
