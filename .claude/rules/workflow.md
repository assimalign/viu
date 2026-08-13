# Workflow

## Commits and branches

- Conventional commits: `type(scope): subject` with `feat|fix|docs|refactor|test|chore`.
- Branches: `main` (production), `feature/{name}`, `fix/{name}`, `docs/{name}`.
  Work tracked in the Viu GitHub Project uses `feature/<wbs>-<slug>`
  (e.g. `feature/V01.01.02.01-dependency-engine`) — the WBS in the branch names the feature in flight.

## GitHub Project execution metadata

- Work items follow `[<wbs>] <title>` (area epic `V01.01.NN` → feature `V01.01.NN.MM` → task
  `V01.01.NN.MM.PP`) in org Project **#15 "Viu"**. Use the `viu-work-items` skill to create,
  place, and link items — especially for capturing scope creep discovered mid-branch.
- **Priority**: lower number = higher priority (P001 before P002).
- **Wave**: lower number = earlier delivery (W01 = rendering foundation … W06 = enterprise polish).
- Autonomous work selection prefers unblocked items in the earliest Priority, then Wave. Conflict
  order: explicit user instruction → dependency/blocker relationships → Priority → Wave.
- The GitHub issue body is the authoritative source of a work item's requirements.
- Project #15 is viu-only. If `assimalign/cohesion` items (`[Lxx...]` codes) ever appear on it,
  flag it and never modify them from this repo.

## Backlog authoring

- Issue bodies must carry enough architectural boundary guidance for a future session to implement
  without this conversation's context: the target `Assimalign.Viu.<Area>` project, allowed
  dependency direction, and any interop/AOT/source-generator boundaries.
- Library layout is area-based and inverted:
  `libraries/<Area>/Assimalign.Viu.<Name>/{src,test}` for publicly consumable runtime and parser
  projects, and `tooling/<Area>/Assimalign.Viu.<Name>/{src,test}` for compiler and editor
  implementation projects. The area location carries the role without changing the assembly id or
  namespace. `tooling/Assimalign.Viu.UtilityCss/{src,test}` is a parked, non-packable root-level
  exception pending an add-on redesign under `libraries/Utilities/`.
- Preserve later-wave requirements in planning notes even when implementing only current-wave scope.
  If a ticket needs prerequisite work from another ticket, call that out rather than silently
  reordering.

## Hard constraints (every change)

- Trimming-safe and WASM/NativeAOT-compatible: no reflection-based serialization, no dynamic code
  generation, no linker-unfriendly activation paths. Roslyn source generators are the sanctioned path.
- The JS-interop boundary is the dominant performance cost — prefer batched interop over per-op
  calls, and always clean up JS-side handles and event listeners.
- `docs/SPECIFICATION.md` is the authority for Viu's semantics: cite the clause id in the issue, code
  comment, or test that pins the behavior. An external project is authoritative only where the issue
  names a declared compatibility target (the `.vue` container format, the Language Server Protocol,
  or a WHATWG/W3C specification); performance ideas borrowed from other frameworks are recorded in
  `docs/PERFORMANCE-RESEARCH.md` and never make that framework normative. Tailwind CSS v4.3.3 may be
  cited only by work explicitly scoped to the parked utility-CSS add-on or its future redesign, never
  as a Viu core target.
