---
paths:
  - "**/*.md"
  - "**/docs/**"
  - "**/*.cs"
---

# Documentation

- **XML doc comments on every public member.** A Viu doc states three things in Viu's own vocabulary:
  **what** the member does, **what it guarantees** (invariants, ordering, thread affinity, allocation
  behavior), and **why the shape is what it is** where that isn't obvious. Viu is a standalone
  framework; no doc comment may make Viu's behavior *derivative* of another project's.
  - **Banned:** "the C# port of X", "mirrors X", "counterpart of X", "upstream", "parity",
    "faithful", and any `vuejs.org` / `github.com/vuejs` / `router.vuejs.org` URL.
  - **Do not just delete the banned clause.** In many docs it is the only thing carrying the
    semantics (`"Creates a shallow ref (Vue's shallowRef())"`). Replace it with the behavior it stood
    in for: *"Creates a reference cell that notifies only on assignment of a new instance, never on
    mutation of the instance it holds."* A summary that says less after the edit is a regression.
  - **Intent markers must survive.** Where a clause like `(upstream parity)` was encoding "this is
    deliberate, do not 'fix' it", restate the intent as a Viu design decision. Where it pinned a
    frozen value layout (`PatchFlags`, `ShapeFlags`, `SlotStability`, the SSR hydration markers), restate
    it as Viu's own stability guarantee — the layout is a contract with previously compiled output.
- **Pinning behavior.** Behavior is pinned by (a) the prose in the doc comment itself, (b) a
  `[Vxx.xx.xx]` WBS reference to the issue that specified it, and (c) a test asserting the chosen
  behavior. Where [`docs/SPECIFICATION.md`](../../docs/SPECIFICATION.md) contains a clause for the
  behavior, cite it as text — `Specified by <c>[RND-FLAGS-1]</c>.` — never as a URL, so the
  API-reference generator ([V01.01.13.04]) resolves ids to anchors from one mapping. **Do not write a
  clause id the spec does not yet contain.**
- **External links.** `<see href>` is for genuine external standards and for foreign formats Viu
  consumes — W3C UI Events, WHATWG HTML, the Language Server Protocol, and the `.vue`
  single-file-component container format. It is never used to cite another framework as the
  authority for Viu's own behavior. Version-pin format-citation URLs and frame them explicitly, e.g.
  *"Container-format reference for the input this parser accepts: `<see href=…>`"*. Tailwind URLs are
  no longer authorized in Viu XML documentation; they may remain inside the standalone utility-CSS add-on
  tree as references for that add-on's v4.3.3 compatibility target.
- **The `.vue` compatibility surface is a shipping feature, not a legacy reference.** [V01.01.06.09]
  (#250) parses the tag-based `.vue` container so Vue single-file components compile under Viu. Every
  mention of `.vue` files, `VueSingleFileComponent*` types, `SingleFileComponentFormat.Vue`, the
  `viu-vue` document type, `**/*.vue` globs, and `.vue`-format spec compatibility **must be
  preserved** — removing them misdescribes the product. The banned-phrase rules above govern *how
  Viu's own semantics are described*, not the naming of the foreign format Viu reads.
- **Utility CSS is standalone add-on design, not Viu specification.**
  [`docs/UTILITY-CSS-DESIGN.md`](../../docs/UTILITY-CSS-DESIGN.md) is non-normative after the
  2026-08-13 removal of SDK, hot-reload, and editor integration. The independently published engine
  lives at `libraries/Utilities/Assimalign.Viu.UtilityCss`; it remains outside every Viu SDK and
  framework surface, with consumer MSBuild integration arriving separately through [V01.01.12.30]
  (#346). Do not cite the design document or Tailwind CSS as authority for Viu core behavior.
- **Other frameworks are performance research, not specification.** Viu tracks other renderers'
  performance work as an input to its own optimization backlog. That tracking lives in
  [`docs/PERFORMANCE-RESEARCH.md`](../../docs/PERFORMANCE-RESEARCH.md) and in the work items it
  spawns — never in doc comments, and never as a reason a Viu behavior is what it is. An adopted
  technique is documented in Viu's terms and pinned by a Viu benchmark; origin acknowledgement, if
  wanted, goes in `docs/SPECIFICATION.md` § "Prior art and influences", once, centrally.
- Per-library design docs mature into
  `libraries/<Area>/Assimalign.Viu.<Name>/docs/OVERVIEW.md` (what it is) and `docs/DESIGN.md` (why it
  is shaped this way, WASM/AOT constraints, non-goals) — the same pair under
  `tooling/<Area>/Assimalign.Viu.<Name>/docs/` for compiler and editor projects. Keep docs
  current in the same change as the code — a `DESIGN.md` that lags the code actively misleads.
- Repo-level planning lives in `docs/` — `docs/SPECIFICATION.md` is the authoritative statement of
  Viu's semantics; `docs/PLAN.md` is the authoritative delivery narrative (architecture map, founding
  decisions, waves); the GitHub Project **#15** board is the authoritative backlog.
- Markdown docs use whole words and link related rules/issues so a future session can act without this
  conversation's context.
