# Assimalign.Viu.Browser

Browser implements `IVirtualNodeHost<BrowserNodeHandle>` and owns DOM namespace creation, property
versus attribute policy, event listeners, form coercion, class/style handling, transitions, interop
batching, and JavaScript-handle cleanup.

The scaffold exposes that boundary but does not reproduce JavaScript interop. No browser component
factory is needed because built-ins are structural nodes executed by Core.
