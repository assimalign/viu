using System.IO;

namespace Assimalign.Viu.Tooling.LanguageServer;

/// <summary>
/// The outcome of one project-context resolution ([V01.01.12.23], #259): a degradation-ladder
/// state, the owning project, and the failure or recovery detail. The status also composes its own
/// <c>window/logMessage</c> payload so the host reports every state the same way, and its
/// <see cref="StateSignature"/> feeds the host's per-project transition ledger — one notification
/// per state transition, never one per keystroke, with a later re-degradation free to re-warn.
/// </summary>
internal sealed record ViuProjectContextStatus
{
    // Language Server Protocol MessageType values.
    private const int WarningMessageType = 2;
    private const int InformationMessageType = 3;

    private ViuProjectContextStatus(
        ViuProjectContextStatusKind kind,
        string? projectFilePath,
        string? detail)
    {
        Kind = kind;
        ProjectFilePath = projectFilePath;
        Detail = detail;
    }

    /// <summary>Gets the degradation-ladder state.</summary>
    internal ViuProjectContextStatusKind Kind { get; }

    /// <summary>Gets the owning project file path, or <see langword="null"/> for a loose file.</summary>
    internal string? ProjectFilePath { get; }

    /// <summary>Gets the state's failure or recovery detail, when the state carries one.</summary>
    internal string? Detail { get; }

    /// <summary>
    /// Gets the project state signature (status kind plus reason payload) the host compares against
    /// the project's last reported signature: an unchanged signature stays silent, a changed one
    /// reports the transition.
    /// </summary>
    internal string StateSignature => $"{Kind}|{Detail}";

    /// <summary>Gets the silent status for a document with no owning Viu project.</summary>
    internal static ViuProjectContextStatus NoOwningProject { get; } =
        new(ViuProjectContextStatusKind.NoOwningProject, projectFilePath: null, detail: null);

    /// <summary>Creates the active status, optionally carrying a partial-failure detail.</summary>
    /// <param name="projectFilePath">The owning project file path.</param>
    /// <param name="detail">
    /// The partial-failure detail (a dropped project reference), or <see langword="null"/> for a
    /// clean activation.
    /// </param>
    internal static ViuProjectContextStatus SemanticsActive(
        string projectFilePath,
        string? detail = null)
        => new(ViuProjectContextStatusKind.SemanticsActive, projectFilePath, detail);

    /// <summary>Creates the status for a project whose restore artifacts have never been produced.</summary>
    /// <param name="projectFilePath">The owning project file path.</param>
    internal static ViuProjectContextStatus AssetsMissing(string projectFilePath)
        => new(ViuProjectContextStatusKind.AssetsMissing, projectFilePath, detail: null);

    /// <summary>Creates the status for a required reference that could not be resolved.</summary>
    /// <param name="projectFilePath">The owning project file path.</param>
    /// <param name="detail">Exactly what failed and the remedy.</param>
    internal static ViuProjectContextStatus AssetsUnresolvable(
        string projectFilePath,
        string detail)
        => new(ViuProjectContextStatusKind.AssetsUnresolvable, projectFilePath, detail);

    /// <summary>Creates the status for restore artifacts older than the project file.</summary>
    /// <param name="projectFilePath">The owning project file path.</param>
    /// <param name="detail">The staleness observation and the re-restore remedy.</param>
    internal static ViuProjectContextStatus AssetsStale(
        string projectFilePath,
        string detail)
        => new(ViuProjectContextStatusKind.AssetsStale, projectFilePath, detail);

    /// <summary>
    /// Composes the <c>window/logMessage</c> payload for this status. Returns
    /// <see langword="false"/> for <see cref="ViuProjectContextStatusKind.NoOwningProject"/> — a
    /// loose file is silent by design, not an error.
    /// </summary>
    /// <param name="messageType">The Language Server Protocol MessageType value.</param>
    /// <param name="message">The user-facing message text.</param>
    internal bool TryCreateLogMessage(out int messageType, out string message)
    {
        // The friendly name leads and the absolute project path follows in parentheses: two
        // same-named projects in one solution (a second App.csproj) must emit distinguishable
        // messages, which the name alone cannot guarantee.
        var project = ProjectFilePath is null
            ? string.Empty
            : $"{Path.GetFileNameWithoutExtension(ProjectFilePath)} ('{ProjectFilePath}')";
        switch (Kind)
        {
            case ViuProjectContextStatusKind.AssetsMissing:
                messageType = WarningMessageType;
                message =
                    $"Viu semantic IntelliSense is unavailable for {project}: " +
                    "obj\\project.assets.json has not been produced. Build or restore the " +
                    "project once (dotnet build); features recover automatically.";
                return true;

            case ViuProjectContextStatusKind.AssetsUnresolvable:
                messageType = WarningMessageType;
                message = $"Viu semantic IntelliSense is unavailable for {project}: {Detail}.";
                return true;

            case ViuProjectContextStatusKind.AssetsStale:
                messageType = InformationMessageType;
                message = $"Viu semantic IntelliSense for {project} is serving stale results: {Detail}.";
                return true;

            case ViuProjectContextStatusKind.SemanticsActive:
                messageType = InformationMessageType;
                message = Detail is null
                    ? $"Viu semantic IntelliSense active for {project}."
                    : $"Viu semantic IntelliSense active for {project}: {Detail}.";
                return true;

            default:
                messageType = 0;
                message = string.Empty;
                return false;
        }
    }
}
