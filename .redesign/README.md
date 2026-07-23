# Viu abstraction redesign

This directory is an isolated, buildable design workspace for the proposed split of
`Assimalign.Viu.Core`. Nothing under `libraries/`, the root solution, the framework packs, or the
shipping SDK references these projects.

The proposal currently contains four library-shaped areas:

- `Assimalign.Viu.Components`
- `Assimalign.Viu.Reactivity`
- `Assimalign.Viu.State`
- `Assimalign.Viu.Core`

Each area uses the repository's `src/`, `test/`, and `docs/` layout. Build the proposal with:

```powershell
dotnet build .redesign/Assimalign.Viu.Redesign.slnx
dotnet test .redesign/Assimalign.Viu.Components/test
dotnet test .redesign/Assimalign.Viu.Reactivity/test
dotnet test .redesign/Assimalign.Viu.State/test
dotnet test .redesign/Assimalign.Viu.Core/test
```

Read [DESIGN.md](DESIGN.md) before treating any name or dependency as final. The scaffold is a
discussion artifact, not the implementation plan and not a compatibility promise.

