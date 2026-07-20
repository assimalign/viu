namespace Assimalign.Viu.Router;

/// <summary>
/// The click information a <see cref="RouterLink"/> inspects to decide whether to intercept a
/// navigation — the DOM-free stand-in for the <c>MouseEvent</c> vue-router's <c>guardEvent</c> reads
/// (<c>packages/router/src/RouterLink.ts</c>). The platform-agnostic components never touch the DOM
/// event directly; a host's event bridge constructs this from the native event, and tests construct
/// it directly. The link navigates only for an unmodified primary-button click that has not been
/// prevented.
/// </summary>
public sealed class RouterLinkClickEvent
{
    /// <summary>Creates a click event.</summary>
    /// <param name="button">The pressed mouse button (0 = primary/left, 1 = middle, 2 = secondary/right).</param>
    /// <param name="controlKey">Whether the Control key was held.</param>
    /// <param name="shiftKey">Whether the Shift key was held.</param>
    /// <param name="altKey">Whether the Alt (Option) key was held.</param>
    /// <param name="metaKey">Whether the Meta (Command/Windows) key was held.</param>
    public RouterLinkClickEvent(
        int button = 0,
        bool controlKey = false,
        bool shiftKey = false,
        bool altKey = false,
        bool metaKey = false)
    {
        Button = button;
        ControlKey = controlKey;
        ShiftKey = shiftKey;
        AltKey = altKey;
        MetaKey = metaKey;
    }

    /// <summary>The pressed mouse button — 0 is the primary (left) button that triggers navigation.</summary>
    public int Button { get; }

    /// <summary>Whether the Control key was held (a system modifier suppresses interception).</summary>
    public bool ControlKey { get; }

    /// <summary>Whether the Shift key was held (a system modifier suppresses interception).</summary>
    public bool ShiftKey { get; }

    /// <summary>Whether the Alt (Option) key was held (a system modifier suppresses interception).</summary>
    public bool AltKey { get; }

    /// <summary>Whether the Meta (Command/Windows) key was held (a system modifier suppresses interception).</summary>
    public bool MetaKey { get; }

    /// <summary>Whether any system modifier (Control/Shift/Alt/Meta) was held.</summary>
    public bool HasSystemModifier => ControlKey || ShiftKey || AltKey || MetaKey;

    /// <summary>
    /// Whether <see cref="PreventDefault"/> has been called — a link never intercepts an
    /// already-prevented event (upstream <c>guardEvent</c> bails on <c>e.defaultPrevented</c>).
    /// </summary>
    public bool DefaultPrevented { get; private set; }

    /// <summary>
    /// Records the intent to prevent the browser's default navigation, so the host's event bridge
    /// suppresses the full page load when the synchronous dispatch returns (upstream:
    /// <c>e.preventDefault()</c>).
    /// </summary>
    public void PreventDefault() => DefaultPrevented = true;
}
