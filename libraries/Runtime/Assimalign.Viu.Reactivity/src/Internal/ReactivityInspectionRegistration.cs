using System;

namespace Assimalign.Viu.Reactivity;

/// <summary>Owns one process-local hook installation without retaining its hook after disposal.</summary>
internal sealed class ReactivityInspectionRegistration : IDisposable
{
    private IReactivityInspectionHook? _hook;

    internal ReactivityInspectionRegistration(IReactivityInspectionHook hook) => _hook = hook;

    public void Dispose()
    {
        if (_hook is null)
        {
            return;
        }

        ReactivityInspection.Remove(_hook);
        _hook = null;
    }
}
