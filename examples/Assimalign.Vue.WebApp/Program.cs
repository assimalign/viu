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
    private static VuecsApp? _current;

    private readonly int _rootHandle;
    private readonly BrowserDomAdapter _adapter;
    private readonly VirtualDomRenderer<int> _renderer;
    private readonly Stopwatch _stopwatch = new();
    private readonly VirtualEventHandler _toggleHandler;
    private readonly VirtualEventHandler _resetHandler;

    private string _lastSnapshot = string.Empty;

    public VuecsApp(int rootHandle)
    {
        _rootHandle = rootHandle;
        _adapter = new BrowserDomAdapter();
        _renderer = new VirtualDomRenderer<int>(_adapter);
        _toggleHandler = VirtualNodeFactory.On(Toggle);
        _resetHandler = VirtualNodeFactory.On(Reset);
        _current = this;
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
        _renderer.Render(_rootHandle, BuildView());
    }

    private VirtualNode BuildView()
    {
        var stateLabel = _stopwatch.IsRunning ? "Running" : "Paused";
        var buttonLabel = _stopwatch.IsRunning ? "Pause" : "Start";
        var elapsed = _stopwatch.Elapsed.ToString(@"hh\:mm\:ss");

        return VirtualNodeFactory.Element(
            "section",
            VirtualNodeFactory.Properties(("className", "shell")),
            VirtualNodeFactory.Element(
                "article",
                VirtualNodeFactory.Properties(("className", "card")),
                VirtualNodeFactory.Element("span", VirtualNodeFactory.Properties(("className", "eyebrow")), VirtualNodeFactory.Text("Vuecs Virtual DOM")),
                VirtualNodeFactory.Element("h1", VirtualNodeFactory.Text("Stopwatch rendered from C#")),
                VirtualNodeFactory.Element(
                    "p",
                    VirtualNodeFactory.Properties(("className", "lead")),
                    VirtualNodeFactory.Text("The browser DOM below is created and patched through the new virtual DOM layer.")),
                VirtualNodeFactory.Element(
                    "div",
                    VirtualNodeFactory.Properties(("className", "meter")),
                    VirtualNodeFactory.Element("span", VirtualNodeFactory.Properties(("className", "meter-label")), VirtualNodeFactory.Text("Elapsed")),
                    VirtualNodeFactory.Element("strong", VirtualNodeFactory.Properties(("className", "meter-value")), VirtualNodeFactory.Text(elapsed))),
                VirtualNodeFactory.Element(
                    "div",
                    VirtualNodeFactory.Properties(("className", "status-row")),
                    VirtualNodeFactory.Element("span", VirtualNodeFactory.Properties(("className", "status-pill")), VirtualNodeFactory.Text(stateLabel)),
                    VirtualNodeFactory.Element("span", VirtualNodeFactory.Properties(("className", "status-hint")), VirtualNodeFactory.Text("Patched in-place on every tick"))),
                VirtualNodeFactory.Element(
                    "div",
                    VirtualNodeFactory.Properties(("className", "actions")),
                    VirtualNodeFactory.Element(
                        "button",
                        VirtualNodeFactory.Properties(("className", "primary"), ("onClick", _toggleHandler), ("type", "button")),
                        VirtualNodeFactory.Text(buttonLabel)),
                    VirtualNodeFactory.Element(
                        "button",
                        VirtualNodeFactory.Properties(("className", "secondary"), ("onClick", _resetHandler), ("type", "button")),
                        VirtualNodeFactory.Text("Reset"))),
                VirtualNodeFactory.Element(
                    "pre",
                    VirtualNodeFactory.Properties(("className", "code-sample")),
                    VirtualNodeFactory.Text(HtmlRenderer.Render(
                        VirtualNodeFactory.Element(
                            "button",
                            VirtualNodeFactory.Properties(("className", buttonLabel.ToLowerInvariant()), ("type", "button")),
                            VirtualNodeFactory.Text(buttonLabel)))))));
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
    internal static void DispatchEvent(string callbackId)
    {
        _current?._adapter.Dispatch(callbackId);
    }
}
