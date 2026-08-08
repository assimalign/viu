namespace Assimalign.Viu;

/// <summary>Identifies the current phase of one single-use persistent application lifetime.</summary>
/// <remarks>Specified by <c>[APP-1]</c>.</remarks>
public enum ApplicationState
{
    /// <summary>The context is claimed but execution has not begun.</summary>
    Created,

    /// <summary>The application has claimed execution and is starting its host terminal.</summary>
    Starting,

    /// <summary>The host terminal is mounted and the application is live.</summary>
    Running,

    /// <summary>Graceful shutdown has been requested and cleanup is running.</summary>
    Stopping,

    /// <summary>Graceful shutdown and cleanup completed.</summary>
    Stopped,

    /// <summary>Startup, running work, or stopping cleanup failed.</summary>
    Failed,
}
