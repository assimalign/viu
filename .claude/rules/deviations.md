---
paths:
  - "**/*.cs"
  - "**/*.csproj"
  - "**/*.props"
  - "**/*.targets"
---

# Deviating from these rules

The rules encode deliberate decisions. When a change must break one, follow this protocol rather than
silently complying or silently ignoring it:

1. **Name the rule** explicitly — don't quietly work around it.
2. **Confirm intent** with the user unless they already acknowledged the deviation.
3. **Scope it narrowly** — the exception covers this one case; the next component in the same session
   still follows the original rule.
4. **Document it in code** at the site:
   `// Deviates from the repo <rule name> rule per design decision: <one-line rationale>.`
5. **Surface it** in the change summary / PR description.

Rules that need especially explicit confirmation before deviating:

- **AOT / trimming safety** — no reflection-based serialization, no dynamic code generation.
- **The central build system** — `VuecsProjectReference` / `VuecsPackageReference` (no raw
  `<ProjectReference>` / `<PackageReference>`), the `build/` props/targets, and centralized versioning.
- **Upstream Vue 3 parity** — a behavioral divergence from vuejs/core v3.5 must be intentional, documented
  (in the type's XML docs and, where relevant, a `DESIGN.md` non-goal), and pinned by a test that asserts
  the *chosen* behavior.
