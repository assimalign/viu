# Assimalign.Viu.VisualStudio

This project is the thin, **in-process** Visual Studio client for Viu single-file components: a
classic VSSDK package targeting `net48`, because the editor surfaces it contributes exist only as MEF
exports inside `devenv.exe`. It ships the `viu` content type, the `.pkgdef` claiming the `.viu` file
extension, the Viu classification types and their format definitions, the lexical classifier that
colors a buffer with no round trip, and `ViuLanguageClient`.

Nothing semantic runs here. Semantic completion, diagnostics, navigation, and every future editor
feature belong to the standalone language server the client starts, so the Viu parsers and Roslyn
never load into `devenv.exe` and the same engine serves other editors. The decision record is in
[`../docs/DESIGN.md`](../docs/DESIGN.md).

The project is deliberately absent from `Assimalign.Viu.slnx` — the VSSDK build tasks are .NET
Framework MSBuild tasks that cannot load under `dotnet build`. It is built by
`extensions/VisualStudio/Build.ps1` through Visual Studio's MSBuild, and its test project compiles
the editor-free sources through `<Compile Include>` links rather than referencing it.

## Language-server packaging contract

The extension reads `language-server.json` beside its assembly. The configured path must remain
inside the installed extension directory. The default layout is:

```text
Assimalign.Viu.VisualStudio/
  Assimalign.Viu.VisualStudio.dll
  Assimalign.Viu.VisualStudio.pkgdef
  language-server.json
  LanguageServer/
    win-x64/
      Assimalign.Viu.Tooling.LanguageServer.exe
    win-arm64/
      Assimalign.Viu.Tooling.LanguageServer.exe
```

The extension directory is derived from this assembly's own location, because an in-process MEF part
has no host-supplied installation path.

Set the `ViuLanguageServerPublishPath` MSBuild property to a directory containing the `win-x64` and
`win-arm64` language-server publish folders when building the extension. Its files are copied into
the `LanguageServer/` package folder. The build fails when either required executable is missing.
Use Visual Studio's MSBuild, not `dotnet build` — the VSSDK tasks are .NET Framework tasks:

```powershell
msbuild Assimalign.Viu.VisualStudio.csproj -restore `
  -p:ViuLanguageServerPublishPath=C:\path\to\architecture-specific\publishes
```

When launched, Visual Studio and the server communicate over Language Server Protocol messages on
standard input and standard output. The server must write logs only to standard error or a file.
`ViuLanguageClient` selects the self-contained executable matching the host process architecture, and
every failure path — malformed `language-server.json`, a configured path escaping the extension
directory, an unsupported architecture, a missing executable, a refused start — records a named
reason that Visual Studio surfaces rather than failing silently.

## Debugging

Run `extensions/VisualStudio/Build.ps1` and install the resulting VSIX, then restart Visual Studio and
open a `.viu` file: the Viu colors appear immediately and the language server starts with the buffer.
The project sets `DeployExtension=false`, so no build installs itself into an experimental hive; pass
`-p:DeployExtension=true` for one build to opt into the classic experimental-instance loop.
