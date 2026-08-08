namespace Assimalign.Viu.Components;

/// <summary>
/// The authored instance behind a code-first registration: wraps one
/// <see cref="ComponentSetup"/> delegate as an <see cref="IComponent"/>.
/// </summary>
internal sealed class DelegateComponent : IComponent
{
    private readonly ComponentSetup _setup;

    internal DelegateComponent(ComponentSetup setup)
    {
        _setup = setup;
    }

    public ComponentRenderer Setup(ComponentContext context) => _setup(context);
}
