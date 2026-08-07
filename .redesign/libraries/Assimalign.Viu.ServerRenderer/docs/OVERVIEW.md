# Assimalign.Viu.ServerRenderer

ServerRenderer is an HTML serialization host over the generic Components tree. Component execution
goes through `ComponentHost.RenderAsync` and the public `IComponentRenderScope`; the serializer
walks `scope.Tree`, passes the scope as the parent of nested component operations, and disposes
it. It never creates or aborts Core's internal mounted components.

Built-in control nodes serialize through their invocation's lazy default slot. With scoped CSS
deferred, the serializer reads no style-scope state and emits no scope attributes.

The package owns HTML escaping, element/attribute validity, void and boolean semantics, hydration
markers, and teleport buffers in the full implementation. A future XML serializer would be a
different host package over the same qualified tree.
