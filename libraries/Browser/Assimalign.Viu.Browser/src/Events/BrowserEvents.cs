using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

/// <summary>
/// Provides the qualified event-modifier and key-guard API used by generated browser bindings.
/// </summary>
/// <remarks>
/// Guards run before the wrapped handler. Failed guards skip the handler, while <c>stop</c> and
/// <c>prevent</c> record response intents on the event. The overload set target-types the inline,
/// method-group, parameterless, and task-returning handler shapes accepted by unchanged template
/// authoring. Specified by <c>[SFC-CG-2]</c> and <c>[V01.01.15.02]</c>.
/// </remarks>
public static class BrowserEvents
{
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

    /// <summary>Wraps a value-returning event handler with the named event modifiers.</summary>
    /// <param name="handler">The handler whose return value is discarded.</param>
    /// <param name="modifiers">The unprefixed modifier names in source order.</param>
    /// <returns>An event handler that applies every modifier before invoking the handler.</returns>
    public static Action<BrowserEvent> WithModifiers(
        Func<BrowserEvent, object?> handler,
        params string[] modifiers)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return WithModifiers(
            browserEvent =>
            {
                _ = handler(browserEvent);
            },
            modifiers);
    }

    /// <summary>Wraps a synchronous event handler with the named event modifiers.</summary>
    /// <param name="handler">The handler to guard.</param>
    /// <param name="modifiers">The unprefixed modifier names in source order.</param>
    /// <returns>An event handler that applies every modifier before invoking the handler.</returns>
    public static Action<BrowserEvent> WithModifiers(
        Action<BrowserEvent> handler,
        params string[] modifiers)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(modifiers);
        return browserEvent =>
        {
            foreach (string modifier in modifiers)
            {
                if (!PassesModifierGuard(browserEvent, modifier, modifiers))
                {
                    return;
                }
            }

            handler(browserEvent);
        };
    }

    /// <summary>Wraps a task-returning event handler with the named event modifiers.</summary>
    /// <param name="handler">The task-returning handler to guard.</param>
    /// <param name="modifiers">The unprefixed modifier names in source order.</param>
    /// <returns>A guarded handler that preserves the task returned by the wrapped handler.</returns>
    public static Func<BrowserEvent, Task> WithModifiers(
        Func<BrowserEvent, Task> handler,
        params string[] modifiers)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(modifiers);
        return browserEvent =>
        {
            foreach (string modifier in modifiers)
            {
                if (!PassesModifierGuard(browserEvent, modifier, modifiers))
                {
                    return Task.CompletedTask;
                }
            }

            return handler(browserEvent);
        };
    }

    /// <summary>Wraps a value-returning parameterless handler with event modifiers.</summary>
    /// <param name="handler">The handler whose return value is discarded.</param>
    /// <param name="modifiers">The unprefixed modifier names in source order.</param>
    /// <returns>An event handler that applies every modifier before invoking the handler.</returns>
    public static Action<BrowserEvent> WithModifiers(
        Func<object?> handler,
        params string[] modifiers)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return WithModifiers(
            _ =>
            {
                handler();
            },
            modifiers);
    }

    /// <summary>Wraps a synchronous parameterless handler with event modifiers.</summary>
    /// <param name="handler">The handler to guard.</param>
    /// <param name="modifiers">The unprefixed modifier names in source order.</param>
    /// <returns>An event handler that applies every modifier before invoking the handler.</returns>
    public static Action<BrowserEvent> WithModifiers(
        Action handler,
        params string[] modifiers)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return WithModifiers(_ => handler(), modifiers);
    }

    /// <summary>Wraps a task-returning parameterless handler with event modifiers.</summary>
    /// <param name="handler">The task-returning handler to guard.</param>
    /// <param name="modifiers">The unprefixed modifier names in source order.</param>
    /// <returns>A guarded handler that preserves the task returned by the wrapped handler.</returns>
    public static Func<BrowserEvent, Task> WithModifiers(
        Func<Task> handler,
        params string[] modifiers)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return WithModifiers(_ => handler(), modifiers);
    }

    /// <summary>Wraps a value-returning event handler with the named key guards.</summary>
    /// <param name="handler">The handler whose return value is discarded.</param>
    /// <param name="keys">The lower-hyphenated key names or supported aliases.</param>
    /// <returns>An event handler that invokes the wrapped handler only for a matching key.</returns>
    public static Action<BrowserEvent> WithKeys(
        Func<BrowserEvent, object?> handler,
        params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return WithKeys(
            browserEvent =>
            {
                _ = handler(browserEvent);
            },
            keys);
    }

    /// <summary>Wraps a synchronous event handler with the named key guards.</summary>
    /// <param name="handler">The handler to guard.</param>
    /// <param name="keys">The lower-hyphenated key names or supported aliases.</param>
    /// <returns>An event handler that invokes the wrapped handler only for a matching key.</returns>
    public static Action<BrowserEvent> WithKeys(
        Action<BrowserEvent> handler,
        params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(keys);
        return browserEvent =>
        {
            if (MatchesKey(browserEvent, keys))
            {
                handler(browserEvent);
            }
        };
    }

    /// <summary>Wraps a task-returning event handler with the named key guards.</summary>
    /// <param name="handler">The task-returning handler to guard.</param>
    /// <param name="keys">The lower-hyphenated key names or supported aliases.</param>
    /// <returns>A guarded handler that preserves the task returned by the wrapped handler.</returns>
    public static Func<BrowserEvent, Task> WithKeys(
        Func<BrowserEvent, Task> handler,
        params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(keys);
        return browserEvent => MatchesKey(browserEvent, keys)
            ? handler(browserEvent)
            : Task.CompletedTask;
    }

    /// <summary>Wraps a value-returning parameterless handler with key guards.</summary>
    /// <param name="handler">The handler whose return value is discarded.</param>
    /// <param name="keys">The lower-hyphenated key names or supported aliases.</param>
    /// <returns>An event handler that invokes the wrapped handler only for a matching key.</returns>
    public static Action<BrowserEvent> WithKeys(
        Func<object?> handler,
        params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return WithKeys(
            _ =>
            {
                handler();
            },
            keys);
    }

    /// <summary>Wraps a synchronous parameterless handler with key guards.</summary>
    /// <param name="handler">The handler to guard.</param>
    /// <param name="keys">The lower-hyphenated key names or supported aliases.</param>
    /// <returns>An event handler that invokes the wrapped handler only for a matching key.</returns>
    public static Action<BrowserEvent> WithKeys(
        Action handler,
        params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return WithKeys(_ => handler(), keys);
    }

    /// <summary>Wraps a task-returning parameterless handler with key guards.</summary>
    /// <param name="handler">The task-returning handler to guard.</param>
    /// <param name="keys">The lower-hyphenated key names or supported aliases.</param>
    /// <returns>A guarded handler that preserves the task returned by the wrapped handler.</returns>
    public static Func<BrowserEvent, Task> WithKeys(
        Func<Task> handler,
        params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return WithKeys(_ => handler(), keys);
    }

    private static bool MatchesKey(BrowserEvent browserEvent, string[] keys)
    {
        if (browserEvent.Key.Length == 0)
        {
            return false;
        }

        string eventKey = NameNormalization.Hyphenate(browserEvent.Key).ToLowerInvariant();
        foreach (string key in keys)
        {
            if (string.Equals(key, eventKey, StringComparison.Ordinal)
                || (KeyAliases.TryGetValue(key, out string? alias)
                    && string.Equals(alias, eventKey, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PassesModifierGuard(
        BrowserEvent browserEvent,
        string modifier,
        string[] allModifiers)
    {
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
        BrowserEventModifiers required = BrowserEventModifiers.None;
        foreach (string modifier in modifiers)
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
