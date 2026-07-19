---
paths:
  - "**/*.md"
  - "**/docs/**"
  - "**/*.cs"
---

# Documentation

- **XML doc comments on every public member.** Where a type or member mirrors a Vue 3 concept, name the
  counterpart and link the authoritative reference (vuejs.org or the vuejs/core source file) — e.g.
  "the C# port of Vue 3.5's `computed()`". This is how upstream semantics stay pinned.
- Per-library design docs mature into `libraries/Assimalign.Viu.<Name>/docs/OVERVIEW.md` (what it is) and
  `docs/DESIGN.md` (why it is shaped this way, C#/WASM divergences, non-goals). Keep them current in the
  same change as the code — a `DESIGN.md` that lags the code actively misleads.
- Repo-level planning lives in `docs/` — `docs/PLAN.md` is the authoritative narrative (architecture map,
  founding decisions, waves); the GitHub Project **#15** board is the authoritative backlog.
- Markdown docs use whole words and link related rules/issues so a future session can act without this
  conversation's context.
