namespace Assimalign.Viu;

/// <summary>Owns Core ambient values for one logical execution flow.</summary>
internal sealed class CoreExecutionState
{
    internal RuntimeComponentContext? CurrentComponent { get; set; }

    internal SchedulerExecutionState Scheduler { get; } = new();
}
