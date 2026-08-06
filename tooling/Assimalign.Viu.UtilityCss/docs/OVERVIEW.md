# Assimalign.Viu.UtilityCss

`Assimalign.Viu.UtilityCss` is the shared, build-time engine for **Viu Utilities**. The
source generator, SDK tasks, and Visual Studio language service consume this one assembly so parsing,
generation, completion, and hover do not maintain separate utility vocabularies.

The assembly targets the repository's analyzer framework because it must load in a Roslyn host. It
is deterministic, I/O-free, reflection-free, and absent from the browser runtime framework.
Filesystem discovery, output writes, static-web-asset registration, and browser refresh remain host
responsibilities.

`UtilityCandidateProjectIndex` maintains the ordinal, reference-counted union of per-source scanner
snapshots. Adding a duplicate use or changing only occurrence spans produces no project-level
invalidation; deleting the final source reference removes the candidate. Filesystem watching and
source identity normalization remain host responsibilities.

The compatibility contract is pinned to Tailwind CSS v4.3.3 behavior, including CSS-first
configuration and the v4 candidate grammar. This is an independent C# implementation: it does not
install, invoke, load, bundle, or require Tailwind CSS, Node, PostCSS, Vite, or another Tailwind
integration.

> Viu Utilities is an independent Viu feature compatible with documented Tailwind CSS v4.3.3
> behavior. It is not affiliated with or endorsed by Tailwind Labs.

The authoritative feature boundary, compatibility matrix, dependency direction, and definition of
done are in the repository-level
[`UTILITY-CSS-DESIGN.md`](../../../docs/UTILITY-CSS-DESIGN.md).

`UtilityThemeParser` is the CSS-first entry point for the design-system layer. It accepts CSS plus
optional source identity, content offset, base theme, and cancellation context, then returns an
immutable `UtilityTheme`, deterministic theme-layer CSS, declarations, and recoverable diagnostics.
Pass the resulting theme to the explicit `UtilityCssCompiler.Compile` overload so generation and
editor resolution observe the same overlay and prefix. `UtilityTheme.Default` contains the complete
v4.3.3 default token inventory and the four built-in animation keyframes. All documented namespaces,
including v4.3's `tab-size` and `zoom`, are available through `GetNamespaceTokens`,
`TryGetNamespaceValue`, and `TryGetNamespaceRawValue`.

`UtilityCssLayerEmitter` emits the canonical layer order, all non-reference theme variables,
animation keyframes that are still referenced by the theme, and the complete Viu Preflight base
layer. Its output uses deterministic LF line endings and respects a configured theme prefix.
`UtilityPreflight` is also available independently when a host needs only the unwrapped base rules.
Compatibility data provenance, Tailwind Labs' MIT license, and the trademark boundary are recorded
in [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md). The exact supported and deferred v4.3.3
theme behavior is recorded in [`DESIGN.md`](DESIGN.md).

`UtilityStylesheetParser` is the CSS-first entry point for the directive foundation tracked by
[V01.01.12.17] (#160). It recognizes source-located `@utility`, `@custom-variant`, `@variant`,
`@apply`, and `@reference` directives, plus `--value()`, `--modifier()`, `--default()`,
`--spacing()`, and `--alpha()` calls. The returned model preserves nested and quoted CSS without
performing file I/O, executing plugins, or expanding authored styles. `UtilityDirectiveEmitter`
provides a deterministic projection of valid directives for incremental cache keys and later
semantic stages. The exact parser-versus-transform boundary is recorded in
[`DESIGN.md`](DESIGN.md).

`UtilityProjectStylesheetCompiler` is the executable layer over that syntax model. It registers
static, complex, negative, and functional custom utilities; resolves documented theme, bare,
literal, arbitrary, modifier, fraction, and default modes; rewrites `@apply` and `@variant`; expands
selector and `@slot` custom variants; and calculates `--spacing()` and `--alpha()`. Its explicit
`UtilityStylesheetReferenceGraph` contains host-resolved stylesheet content, so references import
custom utility and variant definitions exactly once without copying referenced ordinary CSS or
allowing the pure compiler to access the filesystem. The result keeps rewritten authored CSS and
generated custom utility rules separate for deterministic SDK layer assembly.
