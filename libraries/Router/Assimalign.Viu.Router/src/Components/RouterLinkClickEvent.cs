namespace Assimalign.Viu.Router;

/// <summary>
/// The click information a <see cref="RouterLink"/> inspects to decide whether to intercept a
/// navigation: a DOM-free carrier for the button and modifier-key state of a mouse click. The
/// platform-agnostic components never touch a native DOM event; a host's event bridge constructs
/// this from the native event, and tests construct it directly. The link navigates only for an
/// unmodified primary-button click that has not already been prevented.
/// </summary>
/// <remarks>Specified by <c>[RTR-4]</c> and <c>[RTR-7]</c>.</remarks>
public sealed class RouterLinkClickEvent
{
    /// <summary>Creates a click event.</summary>
    /// <param name="button">The pressed mouse button (0 = primary/left, 1 = middle, 2 = secondary/right).</param>
    /// <param name="modifiers">The system-modifier keys held for the click.</param>
    public RouterLinkClickEvent(
        int button = 0,
        RouterLinkModifiers modifiers = RouterLinkModifiers.None)
    {
        Button = button;
        Modifiers = modifiers;
    }

    /// <summary>The pressed mouse button — 0 is the primary (left) button that triggers navigation.</summary>
    public int Button { get; }

    /// <summary>Whether the Control key was held (a system modifier suppresses interception).</summary>
    public bool ControlKey => (Modifiers & RouterLinkModifiers.Control) != 0;

    /// <summary>Whether the Shift key was held (a system modifier suppresses interception).</summary>
    public bool ShiftKey => (Modifiers & RouterLinkModifiers.Shift) != 0;

    /// <summary>Whether the Alt (Option) key was held (a system modifier suppresses interception).</summary>
    public bool AltKey => (Modifiers & RouterLinkModifiers.Alt) != 0;

    /// <summary>Whether the Meta (Command/Windows) key was held (a system modifier suppresses interception).</summary>
    public bool MetaKey => (Modifiers & RouterLinkModifiers.Meta) != 0;

    /// <summary>Gets the combined set of system-modifier keys held for the click.</summary>
    public RouterLinkModifiers Modifiers { get; }

    /// <summary>Whether any system modifier (Control/Shift/Alt/Meta) was held.</summary>
    public bool HasSystemModifier => Modifiers != RouterLinkModifiers.None;

    /// <summary>
    /// Whether <see cref="PreventDefault"/> has been called — a link never intercepts an
    /// already-prevented event, so an outer handler that already claimed the click wins.
    /// </summary>
    public bool DefaultPrevented { get; private set; }

    /// <summary>
    /// Records the intent to prevent the browser's default navigation, so the host's event bridge
    /// suppresses the full page load when the synchronous dispatch returns. The bridge calls the
    /// native <c>preventDefault()</c>; this type only records the intent.
    /// </summary>
    public void PreventDefault() => DefaultPrevented = true;
}
