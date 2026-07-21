using System;

using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Testing.Benchmarks;

/// <summary>
/// One js-framework-benchmark scenario, expressed as a prepare step (mount the baseline tree) and a
/// measured step (render the next tree). Splitting the two lets the interop harness count only the
/// measured step's node operations (the op log is reset between them) and lets BenchmarkDotNet time only
/// the measured step (prepare runs in its iteration setup). The same scenario object therefore feeds both
/// the deterministic interop-count gate and the wall-clock timings, so the two lenses never drift apart.
/// </summary>
public sealed class InteropScenario
{
    private readonly Action<ScenarioContext> _prepare;
    private readonly Action<ScenarioContext> _measured;

    /// <summary>Creates a scenario.</summary>
    /// <param name="name">The stable scenario id (matches the baseline and the BenchmarkDotNet report).</param>
    /// <param name="description">A one-line human description.</param>
    /// <param name="prepare">Mounts the baseline tree; its ops are excluded from the measurement.</param>
    /// <param name="measured">Renders the next tree; its ops are the measured crossings.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public InteropScenario(string name, string description, Action<ScenarioContext> prepare, Action<ScenarioContext> measured)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(prepare);
        ArgumentNullException.ThrowIfNull(measured);
        Name = name;
        Description = description;
        _prepare = prepare;
        _measured = measured;
    }

    /// <summary>The stable scenario id.</summary>
    public string Name { get; }

    /// <summary>The one-line human description.</summary>
    public string Description { get; }

    /// <summary>
    /// Runs the prepare step and resets the op log, returning the context primed so the next
    /// <see cref="RunMeasuredStep"/> logs only the measured crossings.
    /// </summary>
    /// <param name="variant">The tree shape to render.</param>
    /// <returns>The primed context.</returns>
    public ScenarioContext Prepare(ScenarioVariant variant)
    {
        var context = new ScenarioContext(new TestRenderer(), variant);
        _prepare(context);
        context.Renderer.OperationLog.Reset();
        return context;
    }

    /// <summary>Runs the measured step against a context returned by <see cref="Prepare"/>.</summary>
    /// <param name="context">The primed context.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public void RunMeasuredStep(ScenarioContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _measured(context);
    }

    /// <summary>
    /// Prepares and runs the measured step, returning the interop-count result read from the op log —
    /// the harness path used by the deterministic gate.
    /// </summary>
    /// <param name="variant">The tree shape to render.</param>
    /// <returns>The measured interop counts.</returns>
    public InteropCountResult Measure(ScenarioVariant variant)
    {
        var context = Prepare(variant);
        _measured(context);
        return InteropCountResult.From(Name, context.Renderer.OperationLog);
    }

    /// <summary>
    /// Runs the whole scenario (prepare then measured step) over a fresh renderer and returns the total
    /// crossings — the BenchmarkDotNet timing path. Each call is self-contained (new renderer, no shared
    /// state), so BenchmarkDotNet can invoke it many times per iteration; it times the end-to-end
    /// scenario, the same click-to-settled work js-framework-benchmark times. The returned count is
    /// consumed by the caller so the render is not eliminated as dead code.
    /// </summary>
    /// <param name="variant">The tree shape to render.</param>
    /// <returns>The total node operations the scenario performed.</returns>
    public int Run(ScenarioVariant variant)
    {
        var context = Prepare(variant);
        _measured(context);
        return context.Renderer.OperationLog.Operations.Count;
    }

    /// <summary>Returns <see cref="Name"/> so BenchmarkDotNet labels the parameter with the scenario id.</summary>
    public override string ToString() => Name;
}
