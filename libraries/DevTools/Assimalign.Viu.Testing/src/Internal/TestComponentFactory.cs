using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Testing;

internal sealed class TestComponentFactory : IComponentFactory
{
    private readonly IComponentFactory _components;
    private readonly ComponentRegistration? _root;
    private readonly Dictionary<ComponentReference, ComponentRegistration> _stubs = [];
    private bool _rootResolved;

    internal TestComponentFactory(
        ComponentRegistration? root,
        IComponentFactory components,
        IReadOnlyDictionary<Type, ComponentActivator?> stubs)
    {
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(stubs);
        _root = root;
        _components = components;
        foreach (KeyValuePair<Type, ComponentActivator?> stub in stubs)
        {
            ComponentReference reference = ComponentReference.ForType(stub.Key);
            ComponentActivator activator = stub.Value
                ?? (_ => StubComponent.For(stub.Key));
            _stubs.Add(
                reference,
                new ComponentRegistration(
                    reference,
                    new ComponentContract(displayName: $"{stub.Key.Name} test stub"),
                    activator));
        }
    }

    public ComponentRegistration Resolve(ComponentReference reference)
    {
        return TryResolve(reference, out ComponentRegistration? registration)
            ? registration!
            : throw new InvalidOperationException(
                $"Component reference '{reference}' is not registered for this test mount.");
    }

    public bool TryResolve(
        ComponentReference reference,
        out ComponentRegistration? registration)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (!_rootResolved
            && _root is not null
            && _root.Reference == reference)
        {
            _rootResolved = true;
            registration = _root;
            return true;
        }

        return _stubs.TryGetValue(reference, out registration)
            || _components.TryResolve(reference, out registration);
    }
}
