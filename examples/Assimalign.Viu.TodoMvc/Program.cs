using System;
using System.Threading;
using System.Threading.Tasks;

// The TodoMVC sample's entry point. Everything the app needs to run in the browser lives behind the
// OperatingSystem.IsBrowser() guard so the components and the reactive store (Components/, Todos/)
// stay platform-neutral C# that the sibling test project compiles and drives DOM-free through the
// Assimalign.Viu.Testing in-memory renderer. Only TodoMvcBootstrap touches the browser.
if (OperatingSystem.IsBrowser())
{
    await Assimalign.Viu.TodoMvc.TodoMvcBootstrap.RunAsync();
}

// Keep the WASM main loop alive; rendering is reactive from here.
await Task.Delay(Timeout.Infinite);
