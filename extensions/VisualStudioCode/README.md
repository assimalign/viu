# Visual Studio Code extensions

This area contains independently built Visual Studio Code extension packages.

| Package | Description |
| --- | --- |
| [`viu`](packages/viu) | Viu single-file-component language support and language-server client. |
| [`viu-utilitycss`](packages/viu-utilitycss) | Utility CSS completion, hover, and color support for HTML-based files. |

Build the current package set from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\extensions\VisualStudioCode\Build.ps1
```

The orchestrator builds each package and produces one platform-specific VSIX per package and runtime
identifier under `_out/extensions/VisualStudioCode/<configuration>/Vsix/`. Narrow a local gate to
one platform with `-RuntimeIdentifier win-x64` or one extension with `-PackageName viu`; use
`-SkipVsix` for payload staging and client compilation without packaging. `-Version 10.0.0` stamps a
numeric release version into the packaged manifest without changing the source `package.json`, and
`-PreRelease` marks that VSIX for the Marketplace prerelease channel. `-SkipNodeBuild` retains the
server-payload-only workflow and implies `-SkipVsix`, because `vsce` always runs the client
prepublish script. Package-specific details live with each package.
