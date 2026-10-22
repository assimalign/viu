using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// The static input/output declaration of authored component behavior, readable before
/// activation. Carried by <see cref="ComponentRegistration"/>, never by the instance.
/// </summary>
/// <remarks>
/// Deliberately absent: a runtime-carried style-scope identifier. Scoped CSS is compiler-owned;
/// generated render descriptions stamp the known scope identifier as an ordinary static attribute,
/// so the static contract requires no parallel state. Specified by <c>[STY-1]</c>.
/// Parameters and events are immutable snapshots used before activation. Specified by
/// <c>[CMP-1]</c>, <c>[CMP-4]</c>, <c>[CMP-12]</c>, and <c>[CMP-14]</c>.
/// </remarks>
public sealed class ComponentContract
{
    private readonly bool _hasCompilerRenderCacheSize;
    private readonly int _renderCacheSize;
    private readonly ComponentRenderCacheSizeProvider? _renderCacheSizeProvider;

    /// <summary>
    /// Initializes an immutable component contract without compiler cache-size metadata.
    /// </summary>
    /// <param name="displayName">The optional diagnostic display name.</param>
    /// <param name="flags">Static input-resolution behavior.</param>
    /// <param name="parameters">Declared input parameters.</param>
    /// <param name="events">Declared output events.</param>
    /// <remarks>
    /// This compatibility shape preserves contracts produced before the compiler communicated an
    /// exact render-cache size. New generated contracts use the provider overload, including when
    /// their exact current size is zero. Specified by <c>[SFC-CG-9]</c> and
    /// <c>[SFC-OPT-1]</c>.
    /// </remarks>
    public ComponentContract(
        string? displayName = null,
        ComponentFlags flags = ComponentFlags.InheritFallthroughBindings,
        IEnumerable<ComponentParameter>? parameters = null,
        IEnumerable<ComponentEvent>? events = null)
        : this(
            renderCacheSize: 0,
            renderCacheSizeProvider: null,
            hasCompilerRenderCacheSize: false,
            displayName,
            flags,
            parameters,
            events)
    {
    }

    /// <summary>Initializes an immutable component contract with a fixed exact render-cache size.</summary>
    /// <param name="renderCacheSize">
    /// The non-negative number of per-mount cache slots required by compiled render code.
    /// </param>
    /// <param name="displayName">The optional diagnostic display name.</param>
    /// <param name="flags">Static input-resolution behavior.</param>
    /// <param name="parameters">Declared input parameters.</param>
    /// <param name="events">Declared output events.</param>
    /// <remarks>
    /// Supplying this value even when it is zero lets the runtime distinguish an exact empty cache
    /// from the legacy compatibility path. Generated components use the provider overload so
    /// structural metadata deltas remain method-body-shaped; this fixed form remains for code-first
    /// components. Specified by <c>[SFC-CG-9]</c> and <c>[SFC-OPT-1]</c>.
    /// </remarks>
    public ComponentContract(
        int renderCacheSize,
        string? displayName = null,
        ComponentFlags flags = ComponentFlags.InheritFallthroughBindings,
        IEnumerable<ComponentParameter>? parameters = null,
        IEnumerable<ComponentEvent>? events = null)
        : this(
            renderCacheSize,
            renderCacheSizeProvider: null,
            hasCompilerRenderCacheSize: true,
            displayName,
            flags,
            parameters,
            events)
    {
    }

    /// <summary>
    /// Initializes an immutable component contract whose exact render-cache size is read once per
    /// mount from compiler-generated method-body code.
    /// </summary>
    /// <param name="displayName">The optional diagnostic display name.</param>
    /// <param name="renderCacheSizeProvider">
    /// The non-null provider returning the exact non-negative number of cache slots required by the
    /// currently installed render body.
    /// </param>
    /// <param name="flags">Static input-resolution behavior.</param>
    /// <param name="parameters">Declared input parameters.</param>
    /// <param name="events">Declared output events.</param>
    /// <remarks>
    /// Generated components use this shape so structural template edits update the provider's method
    /// body while the already-instantiated contract and its member surface stay unchanged. Direct
    /// code-first contracts may continue to use the fixed-size overload. Specified by
    /// <c>[SFC-CG-9]</c> and <c>[SFC-OPT-1]</c> ([V01.01.06.14]).
    /// </remarks>
    public ComponentContract(
        string? displayName,
        ComponentRenderCacheSizeProvider renderCacheSizeProvider,
        ComponentFlags flags = ComponentFlags.InheritFallthroughBindings,
        IEnumerable<ComponentParameter>? parameters = null,
        IEnumerable<ComponentEvent>? events = null)
        : this(
            renderCacheSize: 0,
            renderCacheSizeProvider: renderCacheSizeProvider
                ?? throw new ArgumentNullException(nameof(renderCacheSizeProvider)),
            hasCompilerRenderCacheSize: true,
            displayName,
            flags,
            parameters,
            events)
    {
    }

    private ComponentContract(
        int renderCacheSize,
        ComponentRenderCacheSizeProvider? renderCacheSizeProvider,
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
        _renderCacheSize = renderCacheSize;
        _renderCacheSizeProvider = renderCacheSizeProvider;
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
    /// Gets the compiler-declared number of fixed per-mount render-cache slots for the currently
    /// installed render body. The value defaults to zero for contracts created through the
    /// pre-compiler compatibility constructor and is always non-negative.
    /// </summary>
    /// <remarks>
    /// Compiler-generated contracts evaluate their stable provider once for each new mount so a
    /// structural hot-reload delta can change the returned count without reinitializing the static
    /// contract. Fixed-size code-first contracts return their constructor value. Specified by
    /// <c>[SFC-CG-9]</c> and <c>[SFC-OPT-1]</c> ([V01.01.06.14]).
    /// </remarks>
    public int RenderCacheSize
    {
        get
        {
            int renderCacheSize = _renderCacheSizeProvider is null
                ? _renderCacheSize
                : _renderCacheSizeProvider();
            ArgumentOutOfRangeException.ThrowIfNegative(renderCacheSize);
            return renderCacheSize;
        }
    }

    internal bool HasCompilerRenderCacheSize => _hasCompilerRenderCacheSize;
}
