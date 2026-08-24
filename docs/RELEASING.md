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

- A published GitHub Release can publish the two Visual Studio preview VSIXs through the protected
  `visual-studio-marketplace` environment. Its required reviewers and Marketplace credential remain
  the approval boundary.
- The same release can publish the two Visual Studio Code extensions only after the
  `validate-release` matrix succeeds. A non-matrix job resolves one Marketplace version per package
  and emits only the missing package/platform cells. The protected
  `visual-studio-code-marketplace` environment gates those cells with `fail-fast: false`; they
  execute serially so first publication and later target updates cannot race, while every planned
  package/target result remains independently visible.

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
Marketplace versions are derived once per release and package because it accepts only numeric
`MAJOR.MINOR.PATCH` versions and does not allow a package/target version to be republished. For a
canonical `MAJOR.MINOR.PATCH-SUFFIX` prerelease, the Marketplace major stays `MAJOR`, the minor is
the odd lane `MINOR * 2 + 1`, and the Marketplace patch is independent of the canonical patch. The
resolver starts that lane at patch `0`; after a complete five-target version it uses the maximum
published patch plus one.

For example:

- With only the historical `10.0.0` entries present, canonical `10.0.0-beta.3` maps to Marketplace
  `10.1.0` on the pre-release channel.
- After `10.1.0` is complete for all five targets, canonical `10.0.1-rc.1` maps to Marketplace
  `10.1.1` on the pre-release channel.

If the highest patch on the mapped lane exists for only some targets, a rerun reuses that version
and emits only its missing targets; it advances only when the target set is complete. Stable
releases use the even half of the convention, `MINOR * 2`, so future stable canonical `10.0.x`
releases use Marketplace `10.0.N` while prereleases use `10.1.N`. The source `package.json` and lock
file remain unchanged placeholders. A nonempty `ViuVersionSuffix` still adds `--pre-release` to both
`vsce package` and `vsce publish`, while a stable Viu version omits it. The package-level
`preview: true` flag only marks the gallery listing as public preview and does not select the
pre-release update channel. See the
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
reviewers plus the existing organization secret `VS_MARKETPLACE_TOKEN`. The protected release job
uses that token to publish both preview VSIXs:

- `Assimalign.Viu.VisualStudio.9d68bb9d-64dd-4715-8362-164655899684`
- `Assimalign.Viu.VisualStudio.UtilityCss.8fcd5c9a-f62f-467c-8655-b7791c41775b`

The owner deleted the former `assimalign-tooling-vs-viu-extension` listing. A removed listing
permanently retains its VSIX id registration, so the main extension's identity was rotated
(previously `...3c6324dd-...`) — copies installed under the old id must be reinstalled once, since
auto-update does not cross an identity rotation. Neither clean internal
name exists yet, so both Visual Studio listings are first-time creations:

- `viu-visualstudio` ->
  `https://marketplace.visualstudio.com/items?itemName=Assimalign.viu-visualstudio`
- `viu-utilitycss-visualstudio` ->
  `https://marketplace.visualstudio.com/items?itemName=Assimalign.viu-utilitycss-visualstudio`

Publisher-manifest internal names are constrained to `[A-Za-z0-9-]` (no dots). Microsoft documents
`VsixPublisher.exe publish -payload ... -publishManifest ...` as creating a listing when its
internal name does not exist; there is no separate listing-creation command. The existing manifests
already supply the required categories, overview, publisher, and valid free price category. See the
[Visual Studio command-line publishing guide](https://learn.microsoft.com/visualstudio/extensibility/walkthrough-publishing-a-visual-studio-extension-via-command-line?view=vs-2022).

VSIX identities (dotted, above) and Marketplace internal names are different namespaces. The clean
internal names provide the new listing URL slugs, while the unchanged VSIX identities let installed
copies reacquire updates from the new listings; Visual Studio recognizes an update by the same VSIX
ID and a higher version, as documented in the
[Visual Studio extension update guidance](https://learn.microsoft.com/visualstudio/extensibility/how-to-update-a-visual-studio-extension?view=vs-2022).

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
The planned matrix deployments are deliberately serialized. Reviewers should expect the protected
environment approvals to appear one cell at a time when the environment requires approval for every
deployment; a partial-failure rerun contains only the targets still missing at the reused version.

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
Code package/target versions are immutable; the central resolver reuses an incomplete version and
schedules only its missing targets instead of attempting to overwrite published cells.
