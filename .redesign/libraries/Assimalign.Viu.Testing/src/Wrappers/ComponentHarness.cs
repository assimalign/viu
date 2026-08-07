using System;
using System.Collections.Generic;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Testing;

/// <summary>
/// Queries refreshed mounted-component views without retaining Core's mounted engine objects.
/// </summary>
public sealed class ComponentHarness
{
    private readonly Func<IReadOnlyList<MountedComponentView<TestNode>>> _viewProvider;

    /// <summary>Initializes a harness over a renderer-owned view provider.</summary>
    /// <param name="viewProvider">A provider reacquired after every scheduler flush.</param>
    public ComponentHarness(
        Func<IReadOnlyList<MountedComponentView<TestNode>>> viewProvider)
    {
        ArgumentNullException.ThrowIfNull(viewProvider);
        _viewProvider = viewProvider;
    }

    /// <summary>Finds the first currently mounted component instance of the requested type.</summary>
    /// <typeparam name="TComponent">The authored component type.</typeparam>
    /// <returns>The current engine-cached view.</returns>
    public MountedComponentView<TestNode> Find<TComponent>()
        where TComponent : class, IComponent
    {
        foreach (var view in _viewProvider())
        {
            if (view.Instance is TComponent)
            {
                return view;
            }
        }

        throw new InvalidOperationException(
            $"No mounted component of type '{typeof(TComponent)}' exists in the current views.");
    }
}
