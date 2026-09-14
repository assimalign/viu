# ADR-0006: Use docfx as the interim documentation site generator

- **Status:** Accepted
- **Date:** 2026-09-14
- **Work item:** [V01.01.13.05](https://github.com/assimalign/viu/issues/102) (#102)
- **Scope:** Repository documentation staging, navigation, search, and static-site publication.

## Context

The [API-reference pipeline](../api-reference/README.md) delivered under
[V01.01.13.04](https://github.com/assimalign/viu/issues/101) already compiles public XML documentation,
resolves specification clause citations, stamps the package version, stages reader documentation,
and rejects generation warnings. Readers need one documentation home connecting the getting-started
guide, examples, library and SDK documentation, specification, and API reference.

Viu now supports static prerendering for explicit routes through `[SSG-1]`, `[SSG-2]`, `[SSG-3]`,
`[SSG-4]`, `[SSG-5]`, and `[SSG-6]` in the [specification](../SPECIFICATION.md), including the
[base SDK publish target](../../sdks/Assimalign.Viu.Sdk/docs/STATIC-PRERENDER.md). The sibling
[viu-docs application](https://github.com/assimalign/viu-docs) has not yet adopted that target for
the documentation landing page and examples index. That application migration is a separate,
tracked dogfooding milestone of #102. This repository change does not implement it or change any
specified framework behavior.

## Decision

We use **docfx 2.78.5**, pinned in the repository's
[local tool manifest](../../.config/dotnet-tools.json), as the interim static generator for the
whole documentation home. Extend the existing
[`Build-ApiReference.ps1`](../../scripts/Build-ApiReference.ps1) staging pipeline; retain its
directly linked reader documentation rule, stable specification clause anchors, package-version
metadata, and warnings-as-errors gate. Do not create a second documentation generator in this
repository.

Use docfx's bundled templates and client-side search assets. The site is a directory of static
files, including its generated search index; publication needs static hosting and no application
server or remote search service. Keep source documentation in its owning repository or library
folder. The site navigation is Overview, Getting started, Guides, Libraries, Specification, and
API reference.

The [Documentation workflow](../../.github/workflows/api-reference.yml) always checks links and
builds its artifact. Deploy only a successful `main` build through the `github-pages` environment
after the owner sets the repository variable `VIU_DEPLOY_DOCS=true` and enables GitHub Pages with
the GitHub Actions source. Publication permissions belong only to the deployment job.

**Planned migration:** retain #102's dogfooding milestone until `assimalign/viu-docs` consumes the
packaged base SDK's `ViuStaticPrerender` target and renders its landing page and examples index
with Viu under `[SSG-1]` through `[SSG-6]`. That application owns its explicit route list, fresh
request scopes, document shell, and deployment-correct assets. The XML API reference can continue
to use this docfx pipeline and join the same published site.

The swap is triggered by a reviewed `viu-docs` migration demonstrating that its package-only
publish produces root and nested static pages, links to the complete guide/library/specification/API
tree, retains version labels and working static search, and passes link and browser checks at the
intended deployment base. Only then replace the docfx landing page and examples index in the
publication assembly and record the superseding decision. Availability of the prerender API alone
does not complete the milestone.

## Consequences

- Readers get one navigable, searchable site immediately, and contributors extend the existing
  generation and validation path.
- Generation is offline after prerequisite dependency and local-tool restores. Site output stays
  under `_out/api-reference/`; generated documentation does not enter source control.
- docfx is a host-side tooling dependency. It adds no framework runtime dependency or authority
  over Viu semantics; no specification clauses are added, amended, or superseded.
- The repository must maintain its Markdown link gate and keep the pinned generator and action
  versions reviewable. The owner controls publication separately from successful builds.
- The `viu-docs` dogfooding migration remains outstanding under #102. Changes to sibling
  repositories, Pages settings, and repository variables are outside this change.

## Alternatives considered

- **Move the whole site to Viu immediately.** Static prerendering is available, but the sibling
  application must first adopt the packaged target and prove its routes, assets, and browser
  behavior. Coupling that application work to this repository would cross the agreed scope.
- **Add another general-purpose static generator.** This duplicates navigation, version handling,
  search, and link staging already available through docfx and adds a second toolchain to maintain.
- **Publish only the API reference.** This leaves guides, libraries, and the specification without
  a unified reader entry point and does not meet #102's navigation requirement.

## References

- [Documentation-site work item #102](https://github.com/assimalign/viu/issues/102), including its
  outstanding Viu dogfooding milestone.
- [API-reference work item #101](https://github.com/assimalign/viu/issues/101) and
  [generation documentation](../api-reference/README.md).
- [Static-prerender work item #68](https://github.com/assimalign/viu/issues/68),
  [specification](../SPECIFICATION.md) clauses `[SSG-1]` through `[SSG-6]`, and the
  [base SDK integration](../../sdks/Assimalign.Viu.Sdk/docs/STATIC-PRERENDER.md).
- [Delivery plan](../PLAN.md) and [contributor instructions](../CONTRIBUTING.md).
