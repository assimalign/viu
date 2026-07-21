using System;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// The <see cref="Watcher"/> behind <c>WatchEffect(effect)</c> — the C# port of the immediate,
/// self-tracking watcher in Vue 3.5's <c>watchEffect</c>
/// (https://vuejs.org/api/reactivity-core.html#watcheffect). The effect body runs once immediately,
/// tracking every reactive value it reads; each later trigger runs the pending cleanup and re-runs
/// the body. There is no old/new value comparison — any tracked change re-runs the effect.
/// </summary>
internal sealed class EffectWatcher : Watcher
{
    private readonly Action<OnCleanup> _effect;

    internal EffectWatcher(Action<OnCleanup> effect, WatchFlushMode flush, IWatchScheduler? scheduler)
        : base(flush, scheduler, once: false)
    {
        _effect = effect;
        Effect = new ReactiveEffect(RunEffect);
        Initialize();
        try
        {
            Effect.Run();
        }
        catch
        {
            Stop();
            throw;
        }
    }

    /// <inheritdoc />
    protected override void React()
    {
        // Cleanup from the previous run happens before the re-run, untracked (the batch flush is not
        // inside any effect), matching the onCleanup timing of watchEffect.
        RunCleanup();
        Effect.Run();
    }

    private void RunEffect() => _effect(RegisterCleanup);
}
