# Visual Studio language tooling design

## Decision

Viu uses the out-of-process `VisualStudio.Extensibility` model as a thin Visual Studio client and a
standalone Language Server Protocol process as the semantic boundary.

This is a better long-term fit than an in-process Visual Studio SDK language service:

- failures and parser/Roslyn dependency conflicts do not destabilize `devenv.exe`;
- the language engine is reusable by Visual Studio, Visual Studio Code, Rider, and other clients;
- Visual Studio's language-server surface covers completion, hover, diagnostics, navigation,
  formatting, code actions, references, and rename;
- the client-specific layer stays limited to document registration, process lifetime, and editor
  presentation.

The Visual Studio language-server and tagger APIs are still marked preview in the 17.14 extensibility
line. They are isolated in `ViuLanguageServerProvider`, `ViuClassificationTaggerProvider`, and
`ViuClassificationTagger` so a future API migration does not reshape the language service.

## Components

```text
Visual Studio
  -> Assimalign.Viu.VisualStudio (out of process)
       -> classification tagger
       -> stdio Language Server Protocol connection
            -> Assimalign.Viu.LanguageServer
                 -> Assimalign.Viu.LanguageService
                      -> Assimalign.Viu.Syntax.SingleFileComponent
```

`Assimalign.Viu.VisualStudio` performs fast lexical classification using Visual Studio's built-in
classification categories. The out-of-process API cannot currently define custom classification
colors, so user themes remain authoritative. Semantic method spans map to the base `identifier`
category and punctuation maps to `operator`: Visual Studio does not register the SDK's `method`
name, while `punctuation` is supplied only when Roslyn editor features are present. These fallbacks
keep the VSIX independent of a particular managed-language workload.

`Assimalign.Viu.LanguageServer` owns protocol framing and translates protocol values into
editor-neutral contracts. It writes protocol messages only to standard output; standard error is
reserved for diagnostics.

`Assimalign.Viu.LanguageService` caches the current text and container parse for each open document.
The MVP exposes block diagnostics, completion catalogs, and hover documentation. It does not load a
Visual Studio solution or project.

## Semantic IntelliSense roadmap

Project-aware IntelliSense requires one authoritative `.viu` to C# projection and source map:

1. Extract the generator's component-name, script-region, generated-context, and source-mapping logic
   into a shared `Assimalign.Viu.Tooling.SingleFileComponent` library.
2. Have both the source generator and language service consume that projection builder so editor and
   compiler behavior cannot drift.
3. Load the containing project through `MSBuildWorkspace` in the language-server process.
4. Add the projected partial component as a synthetic Roslyn document.
5. Map Roslyn completion, hover, signature help, definitions, references, and diagnostics back to the
   original `@script` block and template-expression spans.
6. Integrate the existing template and CSS syntax trees for precise semantic tokens and recoverable
   embedded-language diagnostics.

Parsing remains cancellable and off the Visual Studio UI path. Before enabling whole-project semantic
analysis, the server needs snapshot caching, edit debouncing, and per-document cancellation.

## Packaging

`Build.ps1` publishes self-contained, single-file .NET language-server executables for `win-x64`
and `win-arm64`, then passes their common directory to the extension build through
`ViuLanguageServerPublishPath`. The extension selects the executable matching
`RuntimeInformation.ProcessArchitecture`. This keeps the installed extension independent of a
machine-wide .NET runtime and makes the two architectures declared by the VSIX manifest real
payload guarantees. The VSIX layout is:

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

The server path is resolved relative to the installed extension and rejected if configuration tries
to escape that directory. The host build validates both executable paths before packaging, so a
clean direct build cannot silently emit a VSIX without its language server.

## References

- [VisualStudio.Extensibility overview](https://learn.microsoft.com/visualstudio/extensibility/visualstudio.extensibility/visualstudio-extensibility)
- [Language server provider](https://learn.microsoft.com/visualstudio/extensibility/visualstudio.extensibility/language-server-provider/language-server-provider)
- [Classification tagger walkthrough](https://learn.microsoft.com/visualstudio/extensibility/visualstudio.extensibility/editor/walkthroughs/classification)
- [Visual Studio language configuration](https://learn.microsoft.com/visualstudio/extensibility/language-configuration)
