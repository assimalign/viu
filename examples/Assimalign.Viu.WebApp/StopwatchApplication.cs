using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.WebApp;

// The root component of the demo ([V01.01.03.06]): Setup runs once, closes over refs, and
// returns the render function. The timer starts OnMounted and stops OnUnmounted, so unmounting
// the app tears everything down. The elapsed display is a child component exercising props and
// emits ([V01.01.03.07]/[V01.01.03.08]) live, and the badge is a compiled .viu single-file
// component mounted as a child ([V01.01.06.07]) — the sample's proof that a .viu @template
// component mounts, is reactive, and publishes trimmed + AOT (the budget-gates canary).
internal sealed class StopwatchApplication : IComponent
{
    public string? Name => "StopwatchApplication";

    public ComponentSetup Setup(ComponentProperties properties, ComponentSetupContext context)
    {
        var stopwatch = new Stopwatch();
        var isRunning = Reactive.Reference(false);
        var elapsedText = Reactive.Reference("00:00:00");
        var display = new ElapsedDisplay();
        // The compiled .viu child ([V01.01.06.07]). Instantiated once in Setup (like `display`) so it is
        // a stable component identity across the parent's re-renders — the runtime patches it in place and
        // its @script reactive state (the click count) persists rather than remounting each stopwatch tick.
        var badge = new ViuBadge();
        CancellationTokenSource? ticking = null;

        void Toggle()
        {
            if (stopwatch.IsRunning)
            {
                stopwatch.Stop();
            }
            else
            {
                stopwatch.Start();
            }
            isRunning.Value = stopwatch.IsRunning;
        }

        void Reset()
        {
            if (stopwatch.IsRunning)
            {
                stopwatch.Restart();
            }
            else
            {
                stopwatch.Reset();
            }
            elapsedText.Value = stopwatch.Elapsed.ToString(@"hh\:mm\:ss");
        }

        Lifecycle.OnMounted(() =>
        {
            ticking = new CancellationTokenSource();
            _ = TickAsync(ticking.Token);
        });
        Lifecycle.OnUnmounted(() =>
        {
            ticking?.Cancel();
            ticking = null;
        });

        async Task TickAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100, CancellationToken.None);
                if (stopwatch.IsRunning)
                {
                    // Equal-value writes do not notify: ten ticks a second coalesce to one
                    // re-render per displayed second.
                    elapsedText.Value = stopwatch.Elapsed.ToString(@"hh\:mm\:ss");
                }
            }
        }

        return () => VirtualNodeFactory.Element(
            "section",
            VirtualNodeFactory.Properties(("class", "shell")),
            VirtualNodeFactory.Element(
                "article",
                VirtualNodeFactory.Properties(("class", "card")),
                VirtualNodeFactory.Element("span", VirtualNodeFactory.Properties(("class", "eyebrow")), VirtualNodeFactory.Text("Viu Components")),
                VirtualNodeFactory.Element("h1", VirtualNodeFactory.Text("Stopwatch rendered from C#")),
                VirtualNodeFactory.Element(
                    "p",
                    VirtualNodeFactory.Properties(("class", "lead")),
                    VirtualNodeFactory.Text("A component tree: the display below is a child component fed by props, emitting reset back to its parent.")),
                VirtualNodeFactory.Component(display, VirtualNodeFactory.Properties(
                    ("text", elapsedText.Value),
                    ("running", isRunning.Value),
                    ("onReset", (Action)Reset))),
                VirtualNodeFactory.Element(
                    "div",
                    VirtualNodeFactory.Properties(("class", "actions")),
                    VirtualNodeFactory.Element(
                        "button",
                        VirtualNodeFactory.Properties(("class", "primary"), ("onClick", (Action)Toggle), ("type", "button")),
                        VirtualNodeFactory.Text(isRunning.Value ? "Pause" : "Start"))),
                VirtualNodeFactory.Component(badge)));
    }
}

// The child component: declared props with a default and a validator, a declared emit, and
// lifecycle hooks — the whole W02 contract in one small component.
internal sealed class ElapsedDisplay : IComponent
{
    public string? Name => "ElapsedDisplay";

    public IReadOnlyList<ComponentPropertyDefinition>? Properties { get; } =
    [
        new ComponentPropertyDefinition("text") { DefaultValue = "00:00:00" },
        new ComponentPropertyDefinition("running") { DefaultValue = false },
    ];

    public IReadOnlyList<ComponentEmitDefinition>? Emits { get; } =
    [
        new ComponentEmitDefinition("reset"),
    ];

    public ComponentSetup Setup(ComponentProperties properties, ComponentSetupContext context)
        => () => VirtualNodeFactory.Element(
            "div",
            VirtualNodeFactory.Properties(("class", "meter")),
            VirtualNodeFactory.Element("span", VirtualNodeFactory.Properties(("class", "meter-label")), VirtualNodeFactory.Text("Elapsed")),
            VirtualNodeFactory.Element(
                "strong",
                VirtualNodeFactory.Properties(("class", "meter-value")),
                VirtualNodeFactory.Text(properties.Get<string>("text") ?? "00:00:00")),
            VirtualNodeFactory.Element(
                "span",
                VirtualNodeFactory.Properties(("class", "status-pill")),
                VirtualNodeFactory.Text(properties.Get<bool>("running") ? "Running" : "Paused")),
            VirtualNodeFactory.Element(
                "button",
                VirtualNodeFactory.Properties(
                    ("class", "secondary"),
                    ("type", "button"),
                    ("onClick", (Action)(() => context.Emit("reset")))),
                VirtualNodeFactory.Text("Reset")));
}
