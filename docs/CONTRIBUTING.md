# Documentation conventions

This page defines Viu's documentation system: where each kind of document lives, what belongs in it,
and when it must be updated. It exists so every WBS area documents itself the same way without having
to re-decide. The canonical *working* conventions (C# style, build system, testing) live under
[`.claude/rules/`](../.claude/rules/); this page covers the prose-documentation layout and lifecycle
and complements [`.claude/rules/documentation.md`](../.claude/rules/documentation.md).

## The map

| Document | Location | What it holds |
| --- | --- | --- |
| Root `README.md` | repository root | The project mission, the repository map (every project under `libraries/`, `analyzers/`, `examples/`, `sdks/`, `frameworks/`), and clone/build/run instructions. |
| `PLAN.md` | [`docs/PLAN.md`](PLAN.md) | The authoritative narrative: the Vue 3 → Viu architecture map, the founding design decisions, and the wave strategy. The GitHub [Project #15](https://github.com/orgs/assimalign/projects/15) board is the authoritative *backlog*. |
| Architecture decision records | [`docs/adr/`](adr/) | The append-only log of repo-wide, cross-cutting decisions (see [`adr/README.md`](adr/README.md)). |
| Per-library `OVERVIEW.md` | `libraries/Assimalign.Viu.<Name>/docs/OVERVIEW.md` | What the library **is**. |
| Per-library `DESIGN.md` | `libraries/Assimalign.Viu.<Name>/docs/DESIGN.md` | **Why** the library is shaped the way it is. |
| Per-library topic docs | `libraries/Assimalign.Viu.<Name>/docs/*.md` | Focused specs or local ADRs (e.g. `FORMAT.md`, a library-local `ADR-000N-*.md`). |
| XML doc comments | in source, on every public member | The API-level reference and the pinned Vue 3 counterpart links. |

## What belongs in `OVERVIEW.md`

The reader-facing description of the library. Keep it concise and accurate — describe what exists,
not what is planned.

- **Purpose** — one or two sentences: the role it plays, phrased against its Vue 3 counterpart.
- **Public surface** — the entry points and currency types a consumer touches (the facade, the key
  public types), with a one-line note on each. Not an exhaustive member list — that is the XML docs.
- **Vue 3 package counterpart** — name it and link it (`@vue/<pkg>` on `vuejs.org` or the
  `github.com/vuejs/core/tree/main/packages/<pkg>` path). A scaffold says so plainly.
- **Boundaries** — allowed dependency direction; any interop/AOT/generator constraint; a pointer to
  `DESIGN.md`.

## What belongs in `DESIGN.md`

The rationale and the divergences — why the shape, not the shape itself.

- **Upstream counterpart** — the specific `vuejs/core` package/module this ports, named and linked so
  parity review is mechanical (e.g. `@vue/reactivity`'s `ref.ts`).
- **Design rationale** — the internal structure and the forces behind it (the interop budget, the
  AOT/trimming constraint, the single-threaded model, the incremental-generator caching contract).
- **Known deltas / divergences from Vue 3** — every deliberate departure from upstream, why it was
  forced, and the test that pins the *chosen* behavior. A repo-wide divergence links its ADR under
  [`docs/adr/`](adr/); a library-local one is documented here.
- **Non-goals** — what is intentionally out of scope, sequenced to the work item that will add it.

## When documents must be updated

- **Same change as the code.** An `OVERVIEW.md`/`DESIGN.md` that lags the code actively misleads;
  update it in the commit that changes the public surface or the design it describes.
- **New public type or behavior mirroring Vue 3** — add the XML doc comment with the counterpart
  link, and reflect any new entry point in `OVERVIEW.md`.
- **A deliberate divergence from vuejs/core v3.5** — record it (XML docs + a `DESIGN.md` non-goal or
  delta) and pin it with a test that asserts the chosen behavior (see
  [`.claude/rules/deviations.md`](../.claude/rules/deviations.md)).
- **A cross-cutting or repo-wide decision** — add an ADR (never edit a past one; supersede it — see
  [`adr/README.md`](adr/README.md)).

## Where new things go

- **A new library** — `libraries/Assimalign.Viu.<Name>/{src,test,docs}` (folder name = assembly id,
  no area wrapper folders). Seed `docs/OVERVIEW.md` and `docs/DESIGN.md` with the code. Wire the
  csprojs per [`.claude/rules/build-system.md`](../.claude/rules/build-system.md) ("Adding a new
  library") and add a row to the root `README.md` repository map.
- **A new sample** — `examples/Assimalign.Viu.<Name>/`, referenced from the root `README.md`
  Examples table; keep the demo's own `README` or in-code notes describing what it shows.
- **A new ADR** — copy [`adr/template.md`](adr/template.md) to `adr/NNNN-kebab-title.md` (next number),
  and add it to the [`adr/README.md`](adr/README.md) index.
- **Work items** — every change traces to a `[V01.01.NN…]` WBS item on Project #15; capture
  mid-branch scope creep with the `viu-work-items` skill (see
  [`.claude/rules/workflow.md`](../.claude/rules/workflow.md)).

## Links must resolve

Every relative link in a Markdown doc must point at a real file, and every Vue 3 reference should
point at a stable `vuejs.org` or `vuejs/core` URL. Verify links before committing. (Automated
link-checking in CI is planned under the Documentation area, [V01.01.13]; until it lands, this is a
manual check.)
