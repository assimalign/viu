# Assimalign.Viu.VisualStudio

This project is the thin, out-of-process Visual Studio client for Viu single-file components.
It contributes the `.viu` document type, immediate lexical classification, and a language-server
provider. Semantic completion, diagnostics, navigation, and future editor integrations belong in
the standalone language server so that the same engine can serve other editors.

## Language-server packaging contract

The extension reads `language-server.json` beside its assembly. The configured path must remain
inside the installed extension directory. The default layout is:

```text
Assimalign.Viu.VisualStudio/
  Assimalign.Viu.VisualStudio.dll
  language-server.json
  LanguageServer/
    win-x64/
      Assimalign.Viu.LanguageServer.exe
    win-arm64/
      Assimalign.Viu.LanguageServer.exe
```

Set the `ViuLanguageServerPublishPath` MSBuild property to a directory containing the `win-x64` and
`win-arm64` language-server publish folders when building the extension. Its files are copied into
the `LanguageServer/` package folder. The build fails when either required executable is missing.

```powershell
dotnet build Assimalign.Viu.VisualStudio.csproj `
  -p:ViuLanguageServerPublishPath=C:\path\to\architecture-specific\publishes
```

When launched, Visual Studio and the server communicate over Language Server Protocol messages on
standard input and standard output. The server must write logs only to standard error or a file.
The provider selects the self-contained executable matching the extension-host process architecture.

## Debugging

Build or run this project from Visual Studio and select the generated experimental-instance debug
profile. Open any `.viu` file to activate lexical highlighting and the language server.
