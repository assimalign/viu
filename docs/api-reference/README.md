# Building the API reference

This is the docfx project for [V01.01.13.04](https://github.com/assimalign/viu/issues/101).
Run `pwsh scripts/Build-ApiReference.ps1` from the repository root after the prerequisite
solution restore described in [CONTRIBUTING.md](../CONTRIBUTING.md#build-the-api-reference).

The local tool manifest pins docfx. Its bundled `default` and `modern` templates need no theme
download or CDN. There are no external xref maps, plugins, or build-time network requests after
tool restore. Generated inputs and logs live under `_out/api-reference-work/`; the finished site
lives under `_out/api-reference/`. Neither belongs in source control.
The script disables CLI telemetry and background workload-manifest downloads for its duration,
then restores the caller's environment.

The summary records docfx, SDK, and .NET 10 runtime versions. Metadata uses one canonical copy of
each public assembly and restored dependency. It supplies managed runtime dependencies while
leaving docfx's core/facade choices intact for the mixed `net10.0`/`netstandard2.0` inputs.

The packaging module discovers shipping libraries and rejects inventory drift. All five public
Syntax parsers are added explicitly despite their non-packable project setting. UtilityCss and
FileRouting are included as standalone add-ons. The task-only UtilityCss.Build package remains
covered by the solution's XML compilation gate but contributes no API pages. Tests, analyzers,
compiler/editor implementation projects, language-server executables, SDK tasks and framework
containers are not metadata inputs.

The Templates parser compiles linked copies of Components' `PatchFlags` and `SlotStability`.
It has a separate metadata group and namespace filter; the final API tree keeps the canonical
Components declarations once. Public `EditorBrowsable(Never)` host and generated-code contracts
are documented; internal/private declarations remain excluded.

`Build.Packaging.targets` enables XML files for packable projects, including Release, without
suppressing CS1591. Release alone does not promote CS1591 to an error. This pipeline builds with
warnings as errors and explicitly enables XML output for the otherwise non-packable Syntax
parsers. A fresh `CustomAdditionalCompileInputs` marker, imported through a generated targets file,
forces compilation so cached permissive builds cannot hide missing comments. It avoids solution
`Rebuild`, which cleans SDK staging paths that other solution projects consume. The generated
targets and marker stay under `_out/`; repository build settings remain unchanged. The pipeline
never passes the XML-output switch to test projects.

The specification's paragraph-start clause definitions are the only mapping authority. The
preprocessor adds `clause-` plus the lowercase id as an explicit anchor to the staged specification
and links bracketed clause citations in staged XML to it. Unknown citations and duplicate clause
definitions fail generation. WBS ids such as `[V01.01.13.04]` are work items, not clause ids.
Source XML comments and the normative Markdown remain unchanged.

The site publishes the specification, getting-started guide, developer examples, and their directly
linked reader documentation with repository-relative paths, so docfx validates local links and
fragments. Further repository navigation and agent/contributor source files link to the exact Git
revision after checking that their targets exist locally; they are not recursively published.

The script evaluates `ViuVersion` through MSBuild and injects it into docfx global metadata for
the navigation bar and footer. Generation fails on docfx warnings. Pages deployment is a separate
deliverable, [#102](https://github.com/assimalign/viu/issues/102).
