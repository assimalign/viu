# Releasing Viu

The official release workflow is [`.github/workflows/release.yml`](../.github/workflows/release.yml).
Area workflows only build and test their part of the repository; they never publish.

## Release channels

| Event | NuGet packages | Visual Studio extension |
| --- | --- | --- |
| Merged pull request into `main` | All 20 packages publish to the Assimalign GitHub Packages feed as `X.Y.Z-beta.<run-number>` | Publishes the next numeric VSIX version only when `extensions/VisualStudio/` changed |
| Published stable GitHub Release | All 20 packages publish to nuget.org as the stable release version | No publication |
| Draft or prerelease GitHub Release | No publication | No publication |

The two source generators remain embedded in the SDK and targeting pack. Their projects are
intentionally non-packable and are not separate release packages.

The workflow uses a merged `pull_request_target` event so unmerged and directly pushed commits
cannot publish betas. It checks out the merged commit and verifies that commit is reachable from
`main` before running repository-local build logic. Protect `main` as the complementary repository
control so changes normally enter through reviewed pull requests.

## One-time repository and service setup

### NuGet trusted publishing

Create a trusted publishing policy under the nuget.org owner that owns the `Assimalign.Viu.*`
packages:

- Repository owner: `assimalign`
- Repository: `viu`
- Workflow file: `release.yml`
- Environment: leave blank

The workflow requests a short-lived API key through GitHub OIDC immediately before publishing. It
does not use or require a long-lived NuGet API key. The organization secret `NUGET_USER` must
contain the nuget.org profile name used by the policy, not an email address.

The trusted policy is bound to the workflow filename. Renaming `release.yml` requires updating the
policy before the next release.

### GitHub Packages

Beta packages use the workflow's built-in `GITHUB_TOKEN` with `packages: write`; no package-feed
personal access token is required. `RepositoryUrl` is stamped centrally so the packages associate
with `assimalign/viu`.

GitHub creates a newly published NuGet package as private. If "internal" means organization-wide
internal visibility rather than simply the organization's package feed, change each package to
**Internal** after its first publication and confirm that this repository has Actions write access.
The NuGet push protocol cannot select package visibility.

### Visual Studio Marketplace

Create the `visual-studio-marketplace` GitHub environment and protect it with required reviewers.
The organization secret `VS_MARKETPLACE_TOKEN` must contain an Assimalign Marketplace token with
the least-privilege **Marketplace (publish)** scope.

Visual Studio Marketplace has no per-version prerelease channel comparable to Visual Studio Code.
The entire Viu listing is explicitly marked `Preview=true`, so main builds update one public preview
listing. Supporting stable and opt-in preview versions at the same time would require a second
Marketplace identity and listing.

## Publishing a stable release

1. Set the intended numeric version in
   [`build/Targets/Build.Version.props`](../build/Targets/Build.Version.props). The checked-in suffix
   may remain a preview suffix; the release workflow deliberately produces the stable version.
2. Merge the version change into `main`.
3. Create tag `vMAJOR.MINOR.PATCH` at that commit. The version must exactly match
   `ViuVersionPrefix`, and the tagged commit must be reachable from `main`.
4. Publish a non-draft, non-prerelease GitHub Release for that tag.

The workflow uses the `published` release event so creating a draft cannot publish public packages.
It packs and checksum-verifies the complete set before requesting the temporary nuget.org key.
Duplicate versions are skipped to make a partially completed run safely rerunnable.

After a stable release, advance the canonical patch version before the next main merge. For example,
after `10.0.1`, move to `10.0.2-preview.1`; otherwise a later `10.0.1-beta.*` sorts below the already
published stable version.
