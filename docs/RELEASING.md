# Releasing Viu packages and editor extensions

The official package workflow is [`.github/workflows/release.yml`](../.github/workflows/release.yml).
Area workflows and the shared build action only build and test; they never publish. This keeps every
package publication behind the complete release inventory and its validation gates
([V01.01.12.03]).

## Release flow

A published GitHub Release is the only package trigger. Its tag must be
`vMAJOR.MINOR.PATCH[-PRERELEASE]` and the complete version after `v` must exactly match `ViuVersion`
from [`build/Targets/Build.Version.props`](../build/Targets/Build.Version.props). The tagged commit must
be reachable from `main`.

The workflow performs these stages in order:

1. Every library, tooling project, and source generator runs through the same composite build and
   test action used by area CI.
2. [`scripts/Pack-Release.ps1`](../scripts/Pack-Release.ps1) packs the authoritative inventory with
   warnings as errors, package validation, portable symbols, physical layout checks, and checksums.
3. The validated main packages stage automatically in the Assimalign GitHub Packages feed.
4. The `nuget-org` GitHub environment pauses promotion for its required reviewers. Approval promotes
   the exact downloaded artifact, including `.snupkg` symbol packages, to nuget.org. Rejecting or not
   approving the deployment leaves the release in staging.

Both stable and prerelease versions use this flow. A GitHub Release marked prerelease therefore
stages automatically and can be promoted only by an explicit environment approval; no main-branch
push and no pull-request event can publish a package.

The same workflow has two separately protected editor-extension paths:

- A push to `main` that changes the Visual Studio extensions or their tooling, Syntax,
  ServerRenderer, Utilities, shared-build, or branding inputs can publish the two Visual Studio
  preview VSIXs only when `VIU_PUBLISH_MARKETPLACE` is `true`. The protected
  `visual-studio-marketplace` environment remains the credential and approval boundary. Package
  jobs are explicitly release-only, so this path cannot stage or promote NuGet packages.
- A published GitHub Release can publish the two Visual Studio Code extensions only after the
  `validate-release` matrix succeeds. The protected `visual-studio-code-marketplace` environment
  gates ten independent package/platform cells with `fail-fast: false`. The cells execute serially
  so first publication and later target updates cannot race, while every package/target failure
  remains independently visible.

### Visual Studio Code platform and version contract

The Visual Studio Code Marketplace identifiers are `assimalign.viu` and
`assimalign.viu-utilitycss`. Each extension packages one language-server runtime per VSIX:

| .NET runtime identifier | `vsce` target |
| --- | --- |
| `win-x64` | `win32-x64` |
| `win-arm64` | `win32-arm64` |
| `linux-x64` | `linux-x64` |
| `osx-x64` | `darwin-x64` |
| `osx-arm64` | `darwin-arm64` |

The GitHub tag and NuGet packages continue to use the complete `ViuVersion`. Visual Studio Code
accepts only numeric `MAJOR.MINOR.PATCH` extension versions, so the packaged `extension/package.json`
uses `ViuVersionPrefix`; the source `package.json` and lock file remain unchanged. A nonempty
`ViuVersionSuffix` adds `--pre-release` to both `vsce package` and `vsce publish`, while a stable Viu
version omits it. For example, `10.0.0-beta.2` maps to Visual Studio Code version `10.0.0` on the
pre-release channel.

Marketplace prerelease and regular releases must use distinct numeric versions. After publishing
`10.0.0` as a prerelease, a later regular release therefore cannot reuse `10.0.0`; its
`ViuVersionPrefix` must advance. The package-level `preview: true` flag only marks the gallery
listing as public preview and does not select the prerelease update channel. See the
[Visual Studio Code publishing guide](https://code.visualstudio.com/api/working-with-extensions/publishing-extension#pre-release-extensions).

## Package contract

The [V01.01.12.30] release inventory is exactly 17 main packages and 12 symbol packages, as enforced
by [`scripts/Pack-Release.ps1`](../scripts/Pack-Release.ps1) and
[`scripts/Test-Packages.ps1`](../scripts/Test-Packages.ps1). It includes eleven independently published
libraries, both SDKs, both targeting packs, the Browser runtime pack, and the template pack.
`Assimalign.Viu.UtilityCss` is independently included while remaining outside every Viu SDK and
framework payload. `Assimalign.Viu.DevTools` is
an opt-in library like Router: it is published on its own and is never included in
`Assimalign.Viu.App` or
`Assimalign.Viu.App.Browser`.

Shared build targets stamp every packable project with the repository URL and commit, embedded
`LICENSE`, a package README, deterministic portable PDB settings, and SourceLink data. Executable
library/runtime packages produce a `.snupkg`; content-only packages and these four compile/build-time
containers deliberately do not:

- `Assimalign.Viu.App.Ref` and `Assimalign.Viu.App.Browser.Ref` contain targeting reference assemblies
  and analyzers, while the Browser runtime pack owns the executable framework copies and their symbols.
- `Assimalign.Viu.Sdk` and `Assimalign.Viu.Sdk.Browser` contain MSBuild `Tasks/` and `Watch/` tools,
  whose implementation debugging stays at the repository/build-log boundary rather than the public
  application symbol feed. This is a deliberate tooling-distribution policy; the validator enforces
  that neither SDK produces a symbol package.

The Browser runtime symbol package stores each PDB at
`runtimes/browser-wasm/lib/<tfm>/<assembly>.pdb`, exactly beside the corresponding DLL path in the
main package. The DevTools symbol package likewise stores
`lib/net10.0/Assimalign.Viu.DevTools.pdb`, matching
`lib/net10.0/Assimalign.Viu.DevTools.dll` in its main package. Main packages never duplicate PDB
files. The release validator enforces the deliberate symbol-package inventory and rejects every
`.snupkg` PDB that lacks a DLL, EXE, or WinMD at the same relative path in its `.nupkg`; the package
regression deliberately moves the DevTools PDB to prove that exact-path mismatch is rejected. This
is the path contract used by nuget.org symbol ingestion.

Independently consumable Viu libraries use a compatible-major range:

```text
[current-version,next-major.0.0)
```

Composed products use exact lockstep ranges because their manifests and assets describe one release:

- `Assimalign.Viu.App.Ref`
- `Assimalign.Viu.App.Browser.Ref`
- `Assimalign.Viu.App.Browser.Runtime.browser-wasm`
- `Assimalign.Viu.Sdk.Browser` -> `Assimalign.Viu.Sdk`

The ordinary NuGet package analyzer and .NET package-validation/API-compatibility targets validate
conventional library packages. SDK and shared-framework containers intentionally use `Sdk/`,
`Tasks/`, `ref/`, runtime-pack, and manifest layouts instead of a conventional `lib/` asset; the
release validator checks those layouts, their exact inventory, dependency ranges, analyzer payload,
metadata, and symbols directly. Existing `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` files
remain the source-level API-break gate. A release may also pass
`-PackageValidationBaselineVersion <version>` to compare conventional packages with an already
published baseline.

`Deterministic=true` makes compiled PE/PDB content reproducible. NuGet may vary ZIP timestamps and
package-core metadata between pack invocations, so reproducibility comparisons normalize archive
metadata and compare the extracted payload and manifest rather than raw `.nupkg` bytes.

## One-time repository and service setup

### nuget.org trusted publishing and approval

Create a trusted publishing policy under the nuget.org owner that owns the `Assimalign.Viu.*`
packages:

- Repository owner: `assimalign`
- Repository: `viu`
- Workflow file: `release.yml`
- Environment: `nuget-org`

Create the matching `nuget-org` GitHub environment and configure required reviewers. The workflow
requests a short-lived API key through GitHub OIDC only after approval; it does not use a long-lived
NuGet API key. The organization secret `NUGET_USER` contains the nuget.org profile name used by the
policy, not an email address.

The trusted policy is bound to both the workflow filename and environment. Renaming either requires
updating the policy before the next promotion.

### GitHub Packages staging

The workflow uses its built-in `GITHUB_TOKEN` with `packages: write`; no package-feed personal access
token is required. GitHub Packages is the staging feed, while the release artifact retains symbol
packages for nuget.org promotion.

The repository [`nuget.config`](../nuget.config) names the feed `assimalign-staging` but disables it
for ordinary restore, so authenticated staging cannot affect normal development. To test a staged
package, copy `nuget.config` to a secure temporary location, enable that source in the copy, add a
GitHub Packages credential with `read:packages`, and pass the copy explicitly:

```powershell
$stagingConfig = Join-Path ([System.IO.Path]::GetTempPath()) 'viu-staging.nuget.config'
Copy-Item nuget.config $stagingConfig
dotnet nuget enable source assimalign-staging --configfile $stagingConfig
dotnet nuget update source assimalign-staging --username <github-user> --password <token> --store-password-in-clear-text --configfile $stagingConfig
dotnet restore <consumer-project> --configfile $stagingConfig
```

Delete the temporary configuration after testing. Never add a GitHub token to the repository
configuration.

### Visual Studio Marketplace previews

**Existing owner setup:** keep the `visual-studio-marketplace` GitHub environment and its required
reviewers, the existing organization secret `VS_MARKETPLACE_TOKEN`, and
`VIU_PUBLISH_MARKETPLACE=true` at the repository or organization level. The one protected job uses
that token to publish both preview VSIXs:

- `Assimalign.Viu.VisualStudio.3c6324dd-5c21-46a2-98d1-6b7b5d701f7c`
- `Assimalign.Viu.VisualStudio.UtilityCss.8fcd5c9a-f62f-467c-8655-b7791c41775b`

The second identity is new; the pre-rename VSIX was never published, so no orphaned listing exists
and no cleanup is required.

Publisher-manifest internal names are constrained to `[A-Za-z0-9-]` (no dots) and must match the
existing Marketplace listing exactly for updates. The live "Viu for Visual Studio" listing's
internal name is the legacy `assimalign-tooling-vs-viu-extension`; the Viu Utilities listing is
created on first publish as `assimalign-tooling-vs-viu-utilitycss-extension` for symmetry. VSIX
identities (dotted, above) and Marketplace internal names are different namespaces — the VSIX
identity is what installed copies upgrade by, the internal name is only the listing's URL slug.

### Visual Studio Code Marketplace

**OWNER SETUP REQUIRED:**

1. Create or claim the Visual Studio Code Marketplace publisher id `assimalign`, matching the
   `publisher` field in both extension manifests, and add the publishing Microsoft account to it.
2. Create an Azure DevOps personal access token with organization set to **All accessible
   organizations** and scope **Marketplace (Manage)**.
3. Create the protected GitHub environment `visual-studio-code-marketplace` and configure its
   required reviewers and deployment protection rules. Its deployment branch/tag policy must allow
   the `vMAJOR.MINOR.PATCH[-PRERELEASE]` release tags; a `main`-only policy rejects these jobs.
4. No new secret is required: the job reuses the organization secret `VS_MARKETPLACE_TOKEN`
   (exposed to `vsce` through its conventional `VSCE_PAT` environment variable).

Both marketplaces authenticate with an Azure DevOps PAT, so one organization secret serves both —
provided the PAT carries **All accessible organizations** and **Marketplace (Manage)** scope. A PAT
scoped to a single organization or to publish-only rights fails the VS Code publish; widen the
existing token rather than minting a second credential. Microsoft currently states that global Azure DevOps PATs retire on December 1,
2026, so this requested PAT flow must migrate to Entra-based Marketplace publishing before that
date. Current PAT setup and the migration notice are documented in the
[Visual Studio Code publishing guide](https://code.visualstudio.com/api/working-with-extensions/publishing-extension).
The ten matrix deployments are deliberately serialized to protect first listing creation. Reviewers
should expect the protected-environment approvals to appear one cell at a time when the environment
requires approval for every deployment.

## Publishing a release

1. Set the intended full version once in
   [`build/Targets/Build.Version.props`](../build/Targets/Build.Version.props) and merge it to `main`.
2. Create a tag such as `v10.0.0-alpha.2` or `v10.0.0` at that commit.
3. Publish a GitHub Release for the tag. Mark it prerelease if and only if `ViuVersionSuffix` is
   nonempty; the Visual Studio Code job rejects a mismatch.
4. Confirm the area-test matrix, pack validation, checksum verification, and GitHub Packages staging
   succeed.
5. A required reviewer inspects the staged packages and approves the `nuget-org` deployment when the
   same artifact is ready for public promotion.
6. A required reviewer approves the `visual-studio-code-marketplace` deployment. Each matrix cell
   uploads its platform-specific VSIX before publishing that exact artifact.

`package-order.txt` and `symbol-package-order.txt` enumerate the validated artifacts, and
`checksums.sha256` covers both sets. Duplicate NuGet package versions are skipped, so a partially
completed NuGet publication can be rerun without rebuilding or renumbering packages. Visual Studio
Code package/target versions are immutable and are not silently skipped.
