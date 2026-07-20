using System;
using System.Threading;
using System.Threading.Tasks;

// The forms sample's entry point. The browser-only bootstrap sits behind OperatingSystem.IsBrowser()
// so the reactive model (Composition/) and the components (Components/) stay platform-neutral C# that
// the sibling test project compiles and drives DOM-free. Only FormsBootstrap touches the browser.
if (OperatingSystem.IsBrowser())
{
    await Assimalign.Viu.Forms.FormsBootstrap.RunAsync();
}

// Keep the WASM main loop alive; rendering is reactive from here.
await Task.Delay(Timeout.Infinite);
