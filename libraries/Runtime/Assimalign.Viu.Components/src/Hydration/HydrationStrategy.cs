using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>Describes the host trigger for activating adopted server-rendered markup.</summary>
/// <remarks>
/// A strategy carries data only. It never captures a host node, browser object, or callback, and
/// is safe to retain in an immutable <see cref="ComponentInvocation"/>. Specified by
/// <c>[HYD-LAZY-1]</c> and <c>[HYD-LAZY-2]</c>.
/// </remarks>
public sealed class HydrationStrategy
{
    private static readonly IReadOnlyList<string> NoInteractionEvents =
        Array.AsReadOnly(Array.Empty<string>());

    private HydrationStrategy(
        HydrationStrategyKind kind,
        int? idleTimeoutMilliseconds,
        string? visibilityRootMargin,
        string? mediaQuery,
        IReadOnlyList<string> interactionEvents)
    {
        Kind = kind;
        IdleTimeoutMilliseconds = idleTimeoutMilliseconds;
        VisibilityRootMargin = visibilityRootMargin;
        MediaQuery = mediaQuery;
        InteractionEvents = interactionEvents;
    }

    /// <summary>Gets the eager strategy used when no deferred trigger is declared.</summary>
    public static HydrationStrategy Immediate { get; } = new(
        HydrationStrategyKind.Immediate,
        null,
        null,
        null,
        NoInteractionEvents);

    /// <summary>Gets the trigger kind interpreted by the active hydration host.</summary>
    public HydrationStrategyKind Kind { get; }

    /// <summary>Gets the optional maximum idle delay in milliseconds.</summary>
    public int? IdleTimeoutMilliseconds { get; }

    /// <summary>Gets the optional host visibility root margin.</summary>
    public string? VisibilityRootMargin { get; }

    /// <summary>Gets the media condition required by a media-query strategy.</summary>
    public string? MediaQuery { get; }

    /// <summary>Gets the event names that can activate an interaction strategy.</summary>
    public IReadOnlyList<string> InteractionEvents { get; }

    /// <summary>Creates an idle-turn strategy with an optional maximum delay.</summary>
    /// <param name="timeoutMilliseconds">The optional non-negative fallback delay.</param>
    /// <returns>The immutable idle strategy.</returns>
    public static HydrationStrategy OnIdle(int? timeoutMilliseconds = null)
    {
        if (timeoutMilliseconds is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeoutMilliseconds),
                "The idle hydration timeout cannot be negative.");
        }

        return new HydrationStrategy(
            HydrationStrategyKind.Idle,
            timeoutMilliseconds,
            null,
            null,
            NoInteractionEvents);
    }

    /// <summary>Creates a visibility strategy with an optional host root margin.</summary>
    /// <param name="rootMargin">The optional host visibility root margin.</param>
    /// <returns>The immutable visibility strategy.</returns>
    public static HydrationStrategy OnVisible(string? rootMargin = null) => new(
        HydrationStrategyKind.Visible,
        null,
        rootMargin,
        null,
        NoInteractionEvents);

    /// <summary>Creates a media-query strategy.</summary>
    /// <param name="mediaQuery">The non-empty host media condition.</param>
    /// <returns>The immutable media strategy.</returns>
    public static HydrationStrategy OnMediaQuery(string mediaQuery)
    {
        ArgumentException.ThrowIfNullOrEmpty(mediaQuery);
        return new HydrationStrategy(
            HydrationStrategyKind.MediaQuery,
            null,
            null,
            mediaQuery,
            NoInteractionEvents);
    }

    /// <summary>Creates a first-interaction strategy whose triggering event is replayed.</summary>
    /// <param name="eventNames">One or more non-empty host event names.</param>
    /// <returns>The immutable interaction strategy.</returns>
    public static HydrationStrategy OnInteraction(params string[] eventNames)
    {
        ArgumentNullException.ThrowIfNull(eventNames);
        if (eventNames.Length == 0)
        {
            throw new ArgumentException(
                "At least one interaction event is required.",
                nameof(eventNames));
        }

        string[] copy = new string[eventNames.Length];
        for (int index = 0; index < eventNames.Length; index++)
        {
            ArgumentException.ThrowIfNullOrEmpty(eventNames[index]);
            copy[index] = eventNames[index];
        }

        return new HydrationStrategy(
            HydrationStrategyKind.Interaction,
            null,
            null,
            null,
            Array.AsReadOnly(copy));
    }
}
