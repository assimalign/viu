using System;
using System.Runtime.InteropServices.JavaScript;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Browser;

// A Viu WASM app's whole bootstrap ([V01.01.04.04]): initialize the bridge, create the app,
// mount by selector. The component tree owns its own state and timers from here on.
await BrowserRuntime.InitializeAsync();

// ?diagnostics=1 runs the handle-lifecycle stress checks and the marshaling benchmark
// behind the [V01.01.04.01] ADR instead of the demo.
await JSHost.ImportAsync("benchmark", "/benchmark.js");
if (DiagnosticsInterop.GetQuery().Contains("diagnostics", StringComparison.Ordinal))
{
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

BrowserRuntime.CreateApp(new StopwatchApplication()).Mount("#app");

// Keep the WASM main loop alive; rendering is reactive from here.
await Task.Delay(Timeout.Infinite);
