using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// An opt-in plain-text trace of the editor surfaces the extension participates in
/// ([V01.01.12.07.09]).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it exists.</b> Brace completion happens inside <c>devenv.exe</c>, across parts nothing in
/// this repository owns, and the only reproduction is a user typing. Reading the decompiled editor
/// established what <em>should</em> happen; this establishes what <em>does</em>. It is an
/// investigation aid, not a product feature.
/// </para>
/// <para>
/// <b>Dormant unless asked for.</b> The sink is enabled only when the <c>VIU_EDITOR_DIAGNOSTICS</c>
/// environment variable is non-empty in the process that hosts the editor, which is decided once at
/// type initialization: with the variable unset, <see cref="IsEnabled"/> is <see langword="false"/>,
/// every call site short-circuits on a static field read, and no file is ever created. Because the
/// value is read from the hosting process, it has to be set <em>before</em> Visual Studio starts —
/// <c>devenv.exe</c> inherits the environment of whatever launched it.
/// </para>
/// <para>
/// <b>A diagnostics failure must never reach the editor.</b> Both entry points are total: the message
/// factory is invoked inside a guard, and every file operation is guarded again. A full disk, a
/// locked file, or a bug in a message factory costs a missing log line and nothing else.
/// </para>
/// <para>
/// Editor-free by construction — only <c>System</c> types appear here — so it is compiled into the
/// test project through a <c>&lt;Compile Include&gt;</c> link and its dormancy is unit-tested.
/// </para>
/// </remarks>
internal static class ViuEditorDiagnostics
{
    /// <summary>The environment variable whose non-empty value turns the trace on.</summary>
    public const string EnvironmentVariableName = "VIU_EDITOR_DIAGNOSTICS";

    /// <summary>The file name written under the user's temporary directory.</summary>
    public const string LogFileName = "viu-editor-diagnostics.log";

    // The longest category below, so the message column lines up without a second pass over the file.
    private const int CategoryWidth = 18;

    // Enough of an inserted or deleted run to recognize it; a paste is not what this trace is for.
    private const int DescriptionLimit = 120;

    private static readonly object Gate = new();

    // Null is the whole of the "off" state: resolved once, so an editing session cannot half-enable.
    private static readonly string? ResolvedLogFilePath = ResolveLogFilePath();

    /// <summary>Gets a value indicating whether the trace is on for this process.</summary>
    public static bool IsEnabled => ResolvedLogFilePath is not null;

    /// <summary>Gets the file the trace appends to, or <see langword="null"/> when it is off.</summary>
    public static string? LogFilePath => ResolvedLogFilePath;

    /// <summary>
    /// Appends one line to the trace, building its message only when the trace is on.
    /// </summary>
    /// <param name="category">
    /// The short dotted name of the instrumented point — <c>view.created</c>, <c>typechar.enter</c>.
    /// It is the column readers scan, so it is written before the message rather than inside it.
    /// </param>
    /// <param name="messageFactory">
    /// Produces the message. It runs only when the trace is on, and a throw from it is reported in
    /// place of the message rather than propagated.
    /// </param>
    public static void Trace(string category, Func<string> messageFactory)
    {
        string? logFilePath = ResolvedLogFilePath;
        if (logFilePath is null)
        {
            return;
        }

        string message;
        try
        {
            message = messageFactory() ?? string.Empty;
        }
        catch (Exception exception)
        {
            // The trace is never worth a failed keystroke, so even a broken message factory only
            // costs the message.
            message = "<message factory threw " + exception.GetType().Name + ">";
        }

        Append(logFilePath, category, message);
    }

    /// <summary>
    /// Renders text for the trace: control characters escaped, quoted, and truncated.
    /// </summary>
    /// <param name="text">The text to render; <see langword="null"/> renders as <c>&lt;null&gt;</c>.</param>
    /// <returns>A single-line rendering that cannot break the one-line-per-event file shape.</returns>
    public static string Describe(string? text)
    {
        if (text is null)
        {
            return "<null>";
        }

        StringBuilder builder = new(text.Length + 2);
        builder.Append('"');
        int limit = Math.Min(text.Length, DescriptionLimit);
        for (int index = 0; index < limit; index++)
        {
            char character = text[index];
            switch (character)
            {
                case '\r': builder.Append("\\r"); break;
                case '\n': builder.Append("\\n"); break;
                case '\t': builder.Append("\\t"); break;
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                default:
                    if (character < ' ')
                    {
                        builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        builder.Append('"');
        if (text.Length > limit)
        {
            builder.Append("(+").Append((text.Length - limit).ToString(CultureInfo.InvariantCulture)).Append(" more)");
        }

        return builder.ToString();
    }

    /// <summary>
    /// Renders a character for the trace, so a space or a line break is still legible.
    /// </summary>
    /// <param name="character">The character to render.</param>
    /// <returns>The character in the same escaped, quoted form <see cref="Describe(string?)"/> uses.</returns>
    public static string Describe(char character) => Describe(character.ToString());

    private static void Append(string logFilePath, string category, string message)
    {
        try
        {
            string line = string.Concat(
                DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
                "  ",
                (category ?? string.Empty).PadRight(CategoryWidth),
                "  ",
                message,
                Environment.NewLine);

            // Open, append, close: the trace is low rate and correctness under a crashing host beats
            // holding a handle open across an editing session.
            lock (Gate)
            {
                File.AppendAllText(logFilePath, line, Encoding.UTF8);
            }
        }
        catch (Exception)
        {
            // Deliberately total. A locked file, a full disk, or a revoked temporary directory must
            // cost a log line, never a keystroke.
        }
    }

    private static string? ResolveLogFilePath()
    {
        try
        {
            string? requested = Environment.GetEnvironmentVariable(EnvironmentVariableName);
            return string.IsNullOrEmpty(requested)
                ? null
                : Path.Combine(Path.GetTempPath(), LogFileName);
        }
        catch (Exception)
        {
            // A host that denies environment or temporary-path access simply has no trace.
            return null;
        }
    }
}
