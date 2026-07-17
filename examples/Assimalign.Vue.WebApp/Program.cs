using System;
using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

using Assimalign.Vue.RuntimeCore;

var rootHandle = BrowserDomInterop.QuerySelector("#app");
var app = new VuecsApp(rootHandle);

app.Render();

while (true)
{
    if (app.ShouldAnimate)
    {
        app.Render();
    }

    await Task.Delay(100);
}

internal sealed partial class VuecsApp
{
    private readonly int _rootHandle;
    private readonly Renderer<int> _renderer;
    private readonly Stopwatch _stopwatch = new();
    private readonly Action _toggleHandler;
    private readonly Action _resetHandler;

    private string _lastSnapshot = string.Empty;

    public VuecsApp(int rootHandle)
    {
        _rootHandle = rootHandle;
        _renderer = RendererFactory.CreateRenderer(BrowserRendererOptions.Create());
        _toggleHandler = Toggle;
        _resetHandler = Reset;
    }

    public bool ShouldAnimate => _stopwatch.IsRunning;

    public void Render()
    {
        var snapshot = $"{_stopwatch.IsRunning}:{_stopwatch.Elapsed:hh\\:mm\\:ss}";
        if (string.Equals(_lastSnapshot, snapshot, StringComparison.Ordinal))
        {
            return;
        }

        _lastSnapshot = snapshot;
        _renderer.Render(BuildView(), _rootHandle);
    }

    private VirtualNode BuildView()
    {
        var stateLabel = _stopwatch.IsRunning ? "Running" : "Paused";
        var buttonLabel = _stopwatch.IsRunning ? "Pause" : "Start";
        var elapsed = _stopwatch.Elapsed.ToString(@"hh\:mm\:ss");

        return VirtualNodeFactory.Element(
            "section",
            VirtualNodeFactory.Properties(("class", "shell")),
            VirtualNodeFactory.Element(
                "article",
                VirtualNodeFactory.Properties(("class", "card")),
                VirtualNodeFactory.Element("span", VirtualNodeFactory.Properties(("class", "eyebrow")), VirtualNodeFactory.Text("Vuecs Virtual DOM")),
                VirtualNodeFactory.Element("h1", VirtualNodeFactory.Text("Stopwatch rendered from C#")),
                VirtualNodeFactory.Element(
                    "p",
                    VirtualNodeFactory.Properties(("class", "lead")),
                    VirtualNodeFactory.Text("The browser DOM below is created and patched through the Vue 3 renderer pipeline.")),
                VirtualNodeFactory.Element(
                    "div",
                    VirtualNodeFactory.Properties(("class", "meter")),
                    VirtualNodeFactory.Element("span", VirtualNodeFactory.Properties(("class", "meter-label")), VirtualNodeFactory.Text("Elapsed")),
                    VirtualNodeFactory.Element("strong", VirtualNodeFactory.Properties(("class", "meter-value")), VirtualNodeFactory.Text(elapsed))),
                VirtualNodeFactory.Element(
                    "div",
                    VirtualNodeFactory.Properties(("class", "status-row")),
                    VirtualNodeFactory.Element("span", VirtualNodeFactory.Properties(("class", "status-pill")), VirtualNodeFactory.Text(stateLabel)),
                    VirtualNodeFactory.Element("span", VirtualNodeFactory.Properties(("class", "status-hint")), VirtualNodeFactory.Text("Patched in-place on every tick"))),
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

        Render();
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

        Render();
    }

    [JSExport]
    internal static void DispatchEvent(int nodeHandle, string eventName)
    {
        BrowserRendererOptions.Dispatch(nodeHandle, eventName);
    }
}
