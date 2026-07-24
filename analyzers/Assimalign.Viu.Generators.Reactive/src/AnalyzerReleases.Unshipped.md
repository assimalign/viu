; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
VUER1001 | Assimalign.Viu.Generators.Reactivity | Error | Reactive type must be partial
VUER1002 | Assimalign.Viu.Generators.Reactivity | Error | Reactive type must not be static
VUER1003 | Assimalign.Viu.Generators.Reactivity | Warning | Reactive property must have a getter and a settable setter
VUER1004 | Assimalign.Viu.Generators.Reactivity | Error | Conflicting reactive attributes
