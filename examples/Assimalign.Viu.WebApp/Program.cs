using System;
using System.Runtime.InteropServices.JavaScript;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Browser;

// The benchmark JS module backs the ?diagnostics=1 query check and the marshaling-strategy ops.
await JSHost.ImportAsync("benchmark", "/benchmark.js");

// ?diagnostics=1 runs the handle-lifecycle stress checks and the marshaling benchmark behind the
// [V01.01.04.01] ADR instead of the demo. Diagnostics is advanced tooling that drives the bare bridge
// primitives (a raw renderer, QuerySelector) directly, so it initializes the browser bridge explicitly
// — a normal app never does (MountAsync owns bridge initialization internally).
if (DiagnosticsInterop.GetQuery().Contains("diagnostics", StringComparison.Ordinal))
{
    await BrowserRuntime.InitializeAsync();
    try
    {
        await ViuDiagnostics.RunAsync(BrowserRuntime.QuerySelector("#app"));
    }
    catch (Exception exception)
    {
        DiagnosticsInterop.ReportCrash(exception.ToString());
    }
    await Task.Delay(Timeout.Infinite);
}

// A Viu WASM app's whole bootstrap ([V01.01.03.23]): build the app from a root component and mount it
// by selector. MountAsync loads the browser bridge inside the mount path — there is no separate
// initialization pre-call. The component tree owns its own state and timers from here on.
await BrowserApplication.CreateBuilder().UseRootComponent(new StopwatchApplication()) .Build().MountAsync("#app");

// Keep the WASM main loop alive; rendering is reactive from here.
await Task.Delay(Timeout.Infinite);
