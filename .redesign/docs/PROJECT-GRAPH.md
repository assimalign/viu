# Project graph

Adopted charter: [`../../REDESIGN-REVIEW.md`](../../REDESIGN-REVIEW.md) §2. Vocabulary lives low,
composition lives high, conventions attach through seams.

```mermaid
flowchart TD
    Reactivity["Assimalign.Viu.Reactivity\nchange tracking (leaf)"]
    Components["Assimalign.Viu.Components\nvirtual-node algebra + authored component contract"]
    State["Assimalign.Viu.State\nstore convention (attaches via Services/ambient)"]
    Core["Assimalign.Viu.Core\nApplication Model: engine + operations"]
    Browser["Assimalign.Viu.Browser\nDOM host policy"]
    Server["Assimalign.Viu.ServerRenderer\nHTML serialization host"]
    Testing["Assimalign.Viu.Testing\nmounted-view consumer"]
    Tooling["Compiler.SingleFileComponent\npublic projection facade"]

    Reactivity --> Components
    Reactivity --> State
    Components --> State
    Components --> Core
    Reactivity --> Core
    Core --> Browser
    Core --> Server
    Core --> Testing
```

## Dependency rules

| Project | May reference | Must not know |
|---|---|---|
| Reactivity | Base class library | Components, State, Core, hosts |
| Components | Reactivity | State, Core, hosts — change tracking is intrinsic to the model; conventions are not |
| State | Reactivity, Components | Core, hosts — it attaches Router-style through `Services` and the ambient registry, never a cast or bridge interface |
| Core | Components, Reactivity | Browser and server policy (the shipping composition root may additionally reference State as composition sugar; the contract model does not need the edge) |
| Browser | Components, Core | Core internals |
| ServerRenderer | Components, Core | Core internals and browser DOM handles |
| Testing | Components, Core | Mounted engine implementation types |
| Compiler.SingleFileComponent | Syntax and compiler implementation projects in the real graph | Runtime internals and Roslyn types in its public result |

Hosts reference Core (and transitively Components); no host is a compile-time friend of Core.
`Assimalign.Viu.Shared` is absent. Every former Shared concept moves to a domain owner (the frozen
flag enums and `NameNormalization` land in Components) or is deleted; a miscellaneous abstraction
package is not part of the target graph.
