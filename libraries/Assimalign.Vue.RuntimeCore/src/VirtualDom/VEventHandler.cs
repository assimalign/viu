namespace Assimalign.Vue.RuntimeCore.VirtualDom;

public sealed class VEventHandler
{
    private readonly Action _callback;

    public VEventHandler(Action callback)
    {
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
    }

    public void Invoke()
    {
        _callback();
    }
}
