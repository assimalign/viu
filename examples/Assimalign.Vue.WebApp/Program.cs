using System;
using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

using Assimalign.Vue.Reactivity;
using Assimalign.Vue.RuntimeCore;
using Assimalign.Vue.RuntimeDom;

// The production DOM bridge ([V01.01.04.01]/[V01.01.04.02]) ships with
// Assimalign.Vue.RuntimeDom; the app just initializes it and mounts reactively.
await BrowserRuntime.InitializeAsync();

var rootHandle = BrowserRuntime.QuerySelector("#app");

// ?diagnostics=1 runs the handle-lifecycle stress check and the marshaling-strategy
// benchmark behind the [V01.01.04.01] ADR instead of the stopwatch.
await JSHost.ImportAsync("benchmark", "/benchmark.js");
if (DiagnosticsInterop.GetQuery().Contains("diagnostics", StringComparison.Ordinal))
{
    try
    {
        await VuecsDiagnostics.RunAsync(rootHandle);
    }
    catch (Exception exception)
    {
        DiagnosticsInterop.ReportCrash(exception.ToString());
    }
    return;
}

var app = new VuecsApp(rootHandle);

// Advance the stopwatch STATE on a timer; rendering is driven purely by reactivity
// ([V01.01.03.05] — no polling, no manual render loop).
await app.RunAsync();

internal sealed class VuecsApp
{
    private readonly Reference<bool> _isRunning = Reactive.Reference(false);
    private readonly Reference<string> _elapsedText = Reactive.Reference("00:00:00");
    private readonly Stopwatch _stopwatch = new();
    private readonly Action _toggleHandler;
    private readonly Action _resetHandler;
#pragma warning disable IDE0052 // Held for the app's lifetime: the effect keeps the UI reactive.
    private readonly RenderEffect<int> _renderEffect;
#pragma warning restore IDE0052

    public VuecsApp(int rootHandle)
    {
        _toggleHandler = Toggle;
        _resetHandler = Reset;
        var renderer = BrowserRuntime.CreateRenderer();
        // Mounts immediately and re-renders whenever _isRunning or _elapsedText changes.
        _renderEffect = renderer.CreateRenderEffect(BuildView, rootHandle);
    }

    public async Task RunAsync()
    {
        while (true)
        {
            await Task.Delay(100);
            if (_stopwatch.IsRunning)
            {
                // Equal-value writes do not notify (Vue ref semantics), so ten ticks per
                // second yield at most one re-render per displayed second.
                _elapsedText.Value = FormatElapsed();
            }
        }
    }

    private VirtualNode BuildView()
    {
        var stateLabel = _isRunning.Value ? "Running" : "Paused";
        var buttonLabel = _isRunning.Value ? "Pause" : "Start";

        return VirtualNodeFactory.Element(
            "section",
            VirtualNodeFactory.Properties(("class", "shell")),
            VirtualNodeFactory.Element(
                "article",
                VirtualNodeFactory.Properties(("class", "card")),
                VirtualNodeFactory.Element("span", VirtualNodeFactory.Properties(("class", "eyebrow")), VirtualNodeFactory.Text("Vuecs Reactive Rendering")),
                VirtualNodeFactory.Element("h1", VirtualNodeFactory.Text("Stopwatch rendered from C#")),
                VirtualNodeFactory.Element(
                    "p",
                    VirtualNodeFactory.Properties(("class", "lead")),
                    VirtualNodeFactory.Text("State mutations drive the render effect — the DOM below patches through the production RuntimeDom bridge.")),
                VirtualNodeFactory.Element(
                    "div",
                    VirtualNodeFactory.Properties(("class", "meter")),
                    VirtualNodeFactory.Element("span", VirtualNodeFactory.Properties(("class", "meter-label")), VirtualNodeFactory.Text("Elapsed")),
                    VirtualNodeFactory.Element("strong", VirtualNodeFactory.Properties(("class", "meter-value")), VirtualNodeFactory.Text(_elapsedText.Value))),
                VirtualNodeFactory.Element(
                    "div",
                    VirtualNodeFactory.Properties(("class", "status-row")),
                    VirtualNodeFactory.Element("span", VirtualNodeFactory.Properties(("class", "status-pill")), VirtualNodeFactory.Text(stateLabel)),
                    VirtualNodeFactory.Element("span", VirtualNodeFactory.Properties(("class", "status-hint")), VirtualNodeFactory.Text("Re-rendered reactively — no polling"))),
                VirtualNodeFactory.Element(
                    "div",
                    VirtualNodeFactory.Properties(("class", "actions")),
                    VirtualNodeFactory.Element(
                        "button",
                        VirtualNodeFactory.Properties(("class", "primary"), ("onClick", _toggleHandler), ("type", "button")),
                        VirtualNodeFactory.Text(buttonLabel)),
                    VirtualNodeFactory.Element(
                        "button",
                        VirtualNodeFactory.Properties(("class", "secondary"), ("onClick", _resetHandler), ("type", "button")),
                        VirtualNodeFactory.Text("Reset")))));
    }

    private string FormatElapsed() => _stopwatch.Elapsed.ToString(@"hh\:mm\:ss");

    private void Toggle()
    {
        if (_stopwatch.IsRunning)
        {
            _stopwatch.Stop();
        }
        else
        {
            _stopwatch.Start();
        }

        // State only — the render effect reacts.
        _isRunning.Value = _stopwatch.IsRunning;
    }

    private void Reset()
    {
        if (_stopwatch.IsRunning)
        {
            _stopwatch.Restart();
        }
        else
        {
            _stopwatch.Reset();
        }

        _elapsedText.Value = FormatElapsed();
        _isRunning.Value = _stopwatch.IsRunning;
    }
}
