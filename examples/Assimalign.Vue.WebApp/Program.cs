using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using Assimalign.Vue.RuntimeCore.VirtualDom;

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
    private readonly VEventHandler _toggleHandler;
    private readonly VEventHandler _resetHandler;

    private string _lastSnapshot = string.Empty;

    public VuecsApp(int rootHandle)
    {
        _rootHandle = rootHandle;
        _adapter = new BrowserDomAdapter();
        _renderer = new VirtualDomRenderer<int>(_adapter);
        _toggleHandler = V.On(Toggle);
        _resetHandler = V.On(Reset);
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

    private VNode BuildView()
    {
        var stateLabel = _stopwatch.IsRunning ? "Running" : "Paused";
        var buttonLabel = _stopwatch.IsRunning ? "Pause" : "Start";
        var elapsed = _stopwatch.Elapsed.ToString(@"hh\:mm\:ss");

        return V.H(
            "section",
            V.Props(("className", "shell")),
            V.H(
                "article",
                V.Props(("className", "card")),
                V.H("span", V.Props(("className", "eyebrow")), V.Text("Vuecs Virtual DOM")),
                V.H("h1", V.Text("Stopwatch rendered from C#")),
                V.H(
                    "p",
                    V.Props(("className", "lead")),
                    V.Text("The browser DOM below is created and patched through the new virtual DOM layer.")),
                V.H(
                    "div",
                    V.Props(("className", "meter")),
                    V.H("span", V.Props(("className", "meter-label")), V.Text("Elapsed")),
                    V.H("strong", V.Props(("className", "meter-value")), V.Text(elapsed))),
                V.H(
                    "div",
                    V.Props(("className", "status-row")),
                    V.H("span", V.Props(("className", "status-pill")), V.Text(stateLabel)),
                    V.H("span", V.Props(("className", "status-hint")), V.Text("Patched in-place on every tick"))),
                V.H(
                    "div",
                    V.Props(("className", "actions")),
                    V.H(
                        "button",
                        V.Props(("className", "primary"), ("onClick", _toggleHandler), ("type", "button")),
                        V.Text(buttonLabel)),
                    V.H(
                        "button",
                        V.Props(("className", "secondary"), ("onClick", _resetHandler), ("type", "button")),
                        V.Text("Reset"))),
                V.H(
                    "pre",
                    V.Props(("className", "code-sample")),
                    V.Text(HtmlRenderer.Render(
                        V.H(
                            "button",
                            V.Props(("className", buttonLabel.ToLowerInvariant()), ("type", "button")),
                            V.Text(buttonLabel)))))));
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
