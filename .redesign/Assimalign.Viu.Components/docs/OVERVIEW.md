# Assimalign.Viu.Components

The proposed platform-neutral component-tree vocabulary. Every render-tree value is an
`IComponent`; specialized interfaces describe element, template, text, comment, static, fragment,
and teleport behavior.

The package also owns explicit component activation. `IComponentFactory` implements
`IServiceProvider`, creates a fresh `IComponentTemplate` per mounted template node, and delegates
general service resolution to an externally supplied provider. It does not reference Reactivity,
State, Core, a renderer, or a browser host.

This is a design scaffold. See the root [DESIGN.md](../../DESIGN.md) for the role/lifetime split and
open decisions.

