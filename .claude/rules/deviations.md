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
- **The central build system** — `ViuProjectReference` / `ViuPackageReference` (no raw
  `<ProjectReference>` / `<PackageReference>`), the `build/` props/targets, and centralized versioning.
- **Specified behavior** — diverging from a `docs/SPECIFICATION.md` clause means the clause is wrong or the
  code is: fix one of them. A deliberate change updates the clause in the same commit and is pinned by a
  test asserting the *chosen* behavior.
- **External compatibility targets** — the `.vue` container format, WHATWG HTML serialization, the
  Language Server Protocol, and the WHATWG/W3C specifications Viu implements are contracts with
  outside consumers. A divergence there is a product decision, documented in the owning area's
  `DESIGN.md` and pinned by a conformance test. Tailwind CSS v4.3.3 is only the parked utility-CSS
  add-on's compatibility target and is not part of this Viu core list.
