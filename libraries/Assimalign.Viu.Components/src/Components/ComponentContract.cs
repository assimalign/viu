using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// The static input/output declaration of authored component behavior, readable before
/// activation. Carried by <see cref="ComponentRegistration"/>, never by the instance.
/// </summary>
/// <remarks>
/// Deliberately absent: a style-scope identifier. Scoped CSS is deferred until after the
/// component-model arc; reintroducing it is one additive member here plus serializer and
/// compiler emission — no structural change.
/// Parameters and events are immutable snapshots used before activation. Specified by
/// <c>[CMP-1]</c>, <c>[CMP-4]</c>, <c>[CMP-12]</c>, and <c>[CMP-14]</c>.
/// </remarks>
public sealed class ComponentContract
{
    private readonly bool _hasCompilerRenderCacheSize;

    /// <summary>
    /// Initializes an immutable component contract without compiler cache-size metadata.
    /// </summary>
    /// <param name="displayName">The optional diagnostic display name.</param>
    /// <param name="flags">Static input-resolution behavior.</param>
    /// <param name="parameters">Declared input parameters.</param>
    /// <param name="events">Declared output events.</param>
    /// <remarks>
    /// This compatibility shape preserves contracts produced before the compiler communicated an
    /// exact render-cache size. New generated contracts use the cache-size overload, including
    /// when their exact size is zero. Specified by <c>[SFC-OPT-1]</c>.
    /// </remarks>
    public ComponentContract(
        string? displayName = null,
        ComponentFlags flags = ComponentFlags.InheritFallthroughBindings,
        IEnumerable<ComponentParameter>? parameters = null,
        IEnumerable<ComponentEvent>? events = null)
        : this(
            renderCacheSize: 0,
            hasCompilerRenderCacheSize: false,
            displayName,
            flags,
            parameters,
            events)
    {
    }

    /// <summary>Initializes an immutable component contract with an exact render-cache size.</summary>
    /// <param name="renderCacheSize">
    /// The non-negative number of per-mount cache slots required by compiled render code.
    /// </param>
    /// <param name="displayName">The optional diagnostic display name.</param>
    /// <param name="flags">Static input-resolution behavior.</param>
    /// <param name="parameters">Declared input parameters.</param>
    /// <param name="events">Declared output events.</param>
    /// <remarks>
    /// The compiler supplies this value even when it is zero, allowing the runtime to distinguish
    /// an exact empty cache from the legacy compatibility path. Specified by <c>[SFC-OPT-1]</c>.
    /// </remarks>
    public ComponentContract(
        int renderCacheSize,
        string? displayName = null,
        ComponentFlags flags = ComponentFlags.InheritFallthroughBindings,
        IEnumerable<ComponentParameter>? parameters = null,
        IEnumerable<ComponentEvent>? events = null)
        : this(
            renderCacheSize,
            hasCompilerRenderCacheSize: true,
            displayName,
            flags,
            parameters,
            events)
    {
    }

    private ComponentContract(
        int renderCacheSize,
        bool hasCompilerRenderCacheSize,
        string? displayName,
        ComponentFlags flags,
        IEnumerable<ComponentParameter>? parameters,
        IEnumerable<ComponentEvent>? events)
    {
        if (displayName is not null)
        {
            ArgumentException.ThrowIfNullOrEmpty(displayName);
        }

        if ((flags & ~ComponentFlags.InheritFallthroughBindings) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(flags));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(renderCacheSize);

        DisplayName = displayName;
        Flags = flags;
        Parameters = CollectionSnapshot.CopyNonNull(parameters, nameof(parameters));
        Events = CollectionSnapshot.CopyNonNull(events, nameof(events));
        RenderCacheSize = renderCacheSize;
        _hasCompilerRenderCacheSize = hasCompilerRenderCacheSize;
    }

    /// <summary>Gets the optional diagnostic display name.</summary>
    public string? DisplayName { get; }

    /// <summary>Gets static input-resolution behavior.</summary>
    public ComponentFlags Flags { get; }

    /// <summary>Gets the declared input parameters.</summary>
    public IReadOnlyList<ComponentParameter> Parameters { get; }

    /// <summary>Gets the declared output events.</summary>
    public IReadOnlyList<ComponentEvent> Events { get; }

    /// <summary>
    /// Gets the compiler-declared number of fixed per-mount render-cache slots. The value defaults
    /// to zero for contracts created through the pre-compiler compatibility constructor and is
    /// always non-negative. Specified by <c>[SFC-OPT-1]</c>.
    /// </summary>
    public int RenderCacheSize { get; }

    internal bool HasCompilerRenderCacheSize => _hasCompilerRenderCacheSize;
}
