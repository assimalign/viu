using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>Tracks the generated Hot Reload marker types for one mounted component.</summary>
internal sealed class ComponentHotReloadRegistration : IDisposable
{
    private readonly Action _resetComponentUpdate;
    private readonly Type _templateUpdateMarkerType;
    private readonly Type _scriptUpdateMarkerType;
    private readonly Type _styleUpdateMarkerType;
    private bool _isDisposed;

    internal ComponentHotReloadRegistration(
        Type componentType,
        IComponentHotReloadMetadata metadata,
        Action resetComponentUpdate)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(resetComponentUpdate);
        ArgumentException.ThrowIfNullOrEmpty(metadata.ComponentIdentifier);
        ArgumentNullException.ThrowIfNull(metadata.TemplateUpdateMarkerType);
        ArgumentNullException.ThrowIfNull(metadata.ScriptUpdateMarkerType);
        ArgumentNullException.ThrowIfNull(metadata.StyleUpdateMarkerType);

        ComponentType = componentType;
        _resetComponentUpdate = resetComponentUpdate;
        _templateUpdateMarkerType = metadata.TemplateUpdateMarkerType;
        _scriptUpdateMarkerType = metadata.ScriptUpdateMarkerType;
        _styleUpdateMarkerType = metadata.StyleUpdateMarkerType;
        ComponentIdentifier = metadata.ComponentIdentifier;
    }

    internal Type ComponentType { get; }

    internal string ComponentIdentifier { get; }

    internal ComponentHotReloadChangeKind Classify(
        IReadOnlySet<Type>? updatedTypes)
    {
        if (updatedTypes is null
            || updatedTypes.Contains(_scriptUpdateMarkerType))
        {
            return ComponentHotReloadChangeKind.ScriptReset;
        }

        if (updatedTypes.Contains(_templateUpdateMarkerType))
        {
            return ComponentHotReloadChangeKind.Template;
        }

        if (updatedTypes.Contains(_styleUpdateMarkerType))
        {
            return ComponentHotReloadChangeKind.StyleOnly;
        }

        return updatedTypes.Contains(ComponentType)
            ? ComponentHotReloadChangeKind.ScriptReset
            : ComponentHotReloadChangeKind.None;
    }

    internal void ResetComponentUpdate()
    {
        if (!_isDisposed)
        {
            _resetComponentUpdate();
        }
    }

    /// <summary>Removes this mount from the ambient Hot Reload registry.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        ComponentHotReload.Unregister(this);
    }
}
