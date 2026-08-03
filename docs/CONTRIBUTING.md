# Documentation conventions

This page defines Viu's documentation system: where each kind of document lives, what belongs in it,
and when it must be updated. It exists so every WBS area documents itself the same way without having
to re-decide. The canonical *working* conventions (C# style, build system, testing) live under
[`.claude/rules/`](../.claude/rules/); this page covers the prose-documentation layout and lifecycle
and complements [`.claude/rules/documentation.md`](../.claude/rules/documentation.md).

## The map

| Document | Location | What it holds |
| --- | --- | --- |
| Root `README.md` | repository root | The project mission, the repository map (every project under `libraries/`, `analyzers/`, `sdks/`, `frameworks/`), the external showcase link, and clone/build instructions. |
| `SPECIFICATION.md` | [`docs/SPECIFICATION.md`](SPECIFICATION.md) | **Normative.** What Viu is and what it guarantees, in numbered clauses with stable ids. Highest authority for semantics; every other document below is subordinate to it and must not contradict it. |
| `PLAN.md` | [`docs/PLAN.md`](PLAN.md) | The delivery narrative: the wave strategy, the WBS map, and the founding design decisions. Describes *when*, not *what*. The GitHub [Project #15](https://github.com/orgs/assimalign/projects/15) board is the authoritative *backlog*. |
| Architecture decision records | [`docs/adr/`](adr/) | The append-only log of repo-wide, cross-cutting decisions (see [`adr/README.md`](adr/README.md)). Normative for *rationale*, not for current API shape. |
| `PERFORMANCE-RESEARCH.md` | [`docs/PERFORMANCE-RESEARCH.md`](PERFORMANCE-RESEARCH.md) | The ledger of optimization techniques observed in other rendering frameworks. **Explicitly non-normative**: nothing there constrains Viu until it is measured, adopted, and written into `SPECIFICATION.md` or an ADR. |
| Per-library `OVERVIEW.md` | `libraries/Assimalign.Viu.<Name>/docs/OVERVIEW.md` | What the library **is**. |
| Per-library `DESIGN.md` | `libraries/Assimalign.Viu.<Name>/docs/DESIGN.md` | **Why** the library is shaped the way it is. |
| Per-library topic docs | `libraries/Assimalign.Viu.<Name>/docs/*.md` | Focused specs or local ADRs (e.g. `FORMAT.md`, a library-local `ADR-000N-*.md`). |
| XML doc comments | in source, on every public member | The API-level reference: what each member does, what it guarantees, and why its shape is what it is (see [`.claude/rules/documentation.md`](../.claude/rules/documentation.md)). |

**Precedence.** `SPECIFICATION.md` → the normative delegates it names (the `.viu` `FORMAT.md`,
`UTILITY-CSS-DESIGN.md`) → ADRs → library `DESIGN.md` → `PLAN.md`. A lower-precedence document that
contradicts a higher one is wrong and must be corrected, not reconciled.

## What belongs in `OVERVIEW.md`

The reader-facing description of the library. Keep it concise and accurate — describe what exists,
not what is planned.

- **Purpose** — one or two sentences: the role the library plays in Viu, stated in Viu's own terms.
  A scaffold says so plainly.
- **Public surface** — the entry points and currency types a consumer touches (the facade, the key
  public types), with a one-line note on each. Not an exhaustive member list — that is the XML docs.
- **Specification anchor** — the `SPECIFICATION.md` section and clause ids this library implements,
  cited as text (`[RND-BLOCK-2]`), so a reader can go from the library to the normative statement of
  its behavior. Cite only clauses the specification actually contains.
- **Boundaries** — allowed dependency direction; any interop/AOT/generator constraint; a pointer to
  `DESIGN.md`.

## What belongs in `DESIGN.md`

The rationale and the trade-offs — why the shape, not the shape itself.

- **Design rationale** — the internal structure and the forces behind it (the interop budget, the
  AOT/trimming constraint, the single-threaded model, the incremental-generator caching contract).
- **External compatibility targets** — where the library implements a documented foreign format
  (the `.vue` single-file-component container, Tailwind CSS v4.3.3 for Viu Utilities, WHATWG HTML
  serialization, the Language Server Protocol), name it and link a version-pinned reference. There
  the citation *is* the requirement: it constrains a format Viu deliberately consumes, and it is not
  an authority over Viu's own semantics.
- **Platform adaptations** — where a design that reads oddly is forced by the WASM/AOT/single-thread
  reality rather than chosen, say so and link the test that pins the chosen behavior. A repo-wide
  decision links its ADR under [`docs/adr/`](adr/); a library-local one is documented here.
- **Non-goals** — what is intentionally out of scope, sequenced to the work item that will add it.
  A non-goal that is a *decision* rather than a deferral says so, and matches
  [`SPECIFICATION.md` §17](SPECIFICATION.md#17-non-goals-and-current-limits).

## When documents must be updated

- **Same change as the code.** An `OVERVIEW.md`/`DESIGN.md` that lags the code actively misleads;
  update it in the commit that changes the public surface or the design it describes.
- **A new public type or behavior** — add the XML doc comment (what it does, what it guarantees, why
  the shape), and reflect any new entry point in `OVERVIEW.md`.
- **A change to specified behavior** — the `SPECIFICATION.md` clause, the tests that pin it, and the
  XML docs that cite it move in the same change. A specification change gets the same review as a
  behavior change, because it *is* one (see
  [`.claude/rules/deviations.md`](../.claude/rules/deviations.md)).
- **Behavior that is not yet in the specification** — pin it with a test that asserts the chosen
  behavior and reference the `[V01.01.NN…]` work item that specified it. Do not cite a clause id the
  specification does not yet contain.
- **A cross-cutting or repo-wide decision** — add an ADR (never edit a past one; supersede it — see
  [`adr/README.md`](adr/README.md)).

## Where new things go

- **A new library** — `libraries/Assimalign.Viu.<Name>/{src,test,docs}` (folder name = assembly id,
  no area wrapper folders). Seed `docs/OVERVIEW.md` and `docs/DESIGN.md` with the code. Wire the
  csprojs per [`.claude/rules/build-system.md`](../.claude/rules/build-system.md) ("Adding a new
  library") and add a row to the root `README.md` repository map.
- **A new sample** — add it to
  [`assimalign/viu-examples`](https://github.com/assimalign/viu-examples), where samples consume
  packaged Viu artifacts rather than project references. Update this repository's showcase link
  only when the external entry point changes.
- **A new ADR** — copy [`adr/template.md`](adr/template.md) to `adr/NNNN-kebab-title.md` (next number),
  and add it to the [`adr/README.md`](adr/README.md) index.
- **Work items** — every change traces to a `[V01.01.NN…]` WBS item on Project #15; capture
  mid-branch scope creep with the `viu-work-items` skill (see
  [`.claude/rules/workflow.md`](../.claude/rules/workflow.md)).

## Links must resolve

Every relative link in a Markdown doc must point at a real file, and every in-document anchor must
resolve. An external link is reserved for a genuine standard or for a foreign format Viu consumes,
and is **version-pinned** so it keeps meaning what it meant when it was written. Verify links before
committing. (Automated link-checking in CI is planned under the Documentation area, [V01.01.13];
until it lands, this is a manual check.)

A reference to Viu's own behavior is a `SPECIFICATION.md` clause id written as text — `[SCH-4]`,
never a URL — so the API-reference generator ([V01.01.13.04]) resolves ids to anchors from one
mapping and the docs survive the site moving.
