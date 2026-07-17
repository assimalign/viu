using System;
using System.Collections.Generic;

using Assimalign.Vue.Shared;

namespace Assimalign.Vue.RuntimeDom;

/// <summary>
/// The event-handler modifier and key guards — the C# port of <c>withModifiers</c> and
/// <c>withKeys</c> from <c>@vue/runtime-dom</c>
/// (<c>packages/runtime-dom/src/directives/vOn.ts</c>,
/// https://vuejs.org/guide/essentials/event-handling.html). Guards run before the wrapped
/// handler: a failed guard skips it entirely. <c>.stop</c>/<c>.prevent</c> record intents on
/// the <see cref="BrowserEvent"/>, which the bridge applies to the live JS event when the
/// synchronous dispatch returns.
/// </summary>
public static class BrowserEvents
{
    // Upstream keyNames: aliases whose hyphenated event.key form differs from the alias.
    private static readonly Dictionary<string, string> KeyAliases = new(StringComparer.Ordinal)
    {
        ["esc"] = "escape",
        ["space"] = " ",
        ["up"] = "arrow-up",
        ["left"] = "arrow-left",
        ["right"] = "arrow-right",
        ["down"] = "arrow-down",
        ["delete"] = "backspace",
    };

    /// <summary>
    /// Wraps <paramref name="handler"/> with Vue's event modifiers (upstream:
    /// <c>withModifiers</c>): <c>stop</c>, <c>prevent</c>, <c>self</c>, system modifiers
    /// (<c>ctrl</c>/<c>shift</c>/<c>alt</c>/<c>meta</c>), mouse-button guards
    /// (<c>left</c>/<c>middle</c>/<c>right</c>), and <c>exact</c>.
    /// </summary>
    /// <param name="handler">The handler to guard.</param>
    /// <param name="modifiers">The modifier names, unprefixed (e.g. <c>"stop"</c>, <c>"ctrl"</c>).</param>
    /// <returns>The guarded handler.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> or <paramref name="modifiers"/> is null.</exception>
    public static Action<BrowserEvent> WithModifiers(Action<BrowserEvent> handler, params string[] modifiers)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(modifiers);
        return browserEvent =>
        {
            foreach (var modifier in modifiers)
            {
                if (!PassesModifierGuard(browserEvent, modifier, modifiers))
                {
                    return;
                }
            }
            handler(browserEvent);
        };
    }

    /// <summary>
    /// Wraps <paramref name="handler"/> to run only for the named keys (upstream:
    /// <c>withKeys</c>), matching Vue's key aliases (<c>enter</c>, <c>tab</c>, <c>delete</c>,
    /// <c>esc</c>, <c>space</c>, <c>up</c>, <c>down</c>, <c>left</c>, <c>right</c>) against
    /// the hyphenated <c>event.key</c>.
    /// </summary>
    /// <param name="handler">The handler to guard.</param>
    /// <param name="keys">The key names.</param>
    /// <returns>The guarded handler.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> or <paramref name="keys"/> is null.</exception>
    public static Action<BrowserEvent> WithKeys(Action<BrowserEvent> handler, params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(keys);
        return browserEvent =>
        {
            if (browserEvent.Key.Length == 0)
            {
                return;
            }
            var eventKey = StyleAndClassNormalization.Hyphenate(browserEvent.Key).ToLowerInvariant();
            foreach (var key in keys)
            {
                if (string.Equals(key, eventKey, StringComparison.Ordinal)
                    || (KeyAliases.TryGetValue(key, out var alias)
                        && string.Equals(alias, eventKey, StringComparison.Ordinal)))
                {
                    handler(browserEvent);
                    return;
                }
            }
        };
    }

    private static bool PassesModifierGuard(BrowserEvent browserEvent, string modifier, string[] allModifiers)
    {
        // Upstream modifierGuards parity, one guard per modifier name.
        switch (modifier)
        {
            case "stop":
                browserEvent.StopPropagation();
                return true;
            case "prevent":
                browserEvent.PreventDefault();
                return true;
            case "self":
                return browserEvent.IsSelfTarget;
            case "ctrl":
                return (browserEvent.Modifiers & BrowserEventModifiers.Control) != 0;
            case "shift":
                return (browserEvent.Modifiers & BrowserEventModifiers.Shift) != 0;
            case "alt":
                return (browserEvent.Modifiers & BrowserEventModifiers.Alt) != 0;
            case "meta":
                return (browserEvent.Modifiers & BrowserEventModifiers.Meta) != 0;
            case "left":
                return browserEvent.Button <= 0;
            case "middle":
                return browserEvent.Button is < 0 or 1;
            case "right":
                return browserEvent.Button is < 0 or 2;
            case "exact":
                return MatchesExactly(browserEvent, allModifiers);
            default:
                return true;
        }
    }

    private static bool MatchesExactly(BrowserEvent browserEvent, string[] modifiers)
    {
        // Upstream .exact: every PRESSED system modifier must be listed on the handler.
        var required = BrowserEventModifiers.None;
        foreach (var modifier in modifiers)
        {
            required |= modifier switch
            {
                "ctrl" => BrowserEventModifiers.Control,
                "shift" => BrowserEventModifiers.Shift,
                "alt" => BrowserEventModifiers.Alt,
                "meta" => BrowserEventModifiers.Meta,
                _ => BrowserEventModifiers.None,
            };
        }
        return browserEvent.Modifiers == required;
    }
}
