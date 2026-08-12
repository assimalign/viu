using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

using StreamJsonRpc;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Connects Visual Studio to the standalone Viu language server over the server's standard input and
/// output.
/// </summary>
/// <remarks>
/// <para>
/// The client and its thin classification adapter are the whole of what runs in process for semantic
/// features: they start the packaged executable, hand Visual Studio the two streams, and translate
/// server-authored ranges to classification-type names already registered by the editor. Every
/// language feature and every classification decision is computed in the server process, so the Viu
/// parsers and Roslyn never load into <c>devenv.exe</c>.
/// </para>
/// <para>
/// Activation is scoped to the <c>viu</c> content type alone. <c>.vue</c> is deliberately out of scope
/// here: Visual Studio's Web Tools owns that file extension explicitly, so a <c>.vue</c> buffer never
/// carries a Viu content type — see <see cref="ViuContentTypes"/>.
/// </para>
/// <para>
/// The one initialization option opts this client into exact C# classification-name publications.
/// It carries no project fact: every project input still comes from the document's own file path and
/// restore artifacts. The adapter stores only authored ranges, text checksums, and editor-owned type
/// names; the standard semantic-token response remains available to every other client.
/// </para>
/// </remarks>
[Export(typeof(ILanguageClient))]
[ContentType(ViuContentTypes.Viu)]
internal sealed class ViuLanguageClient :
    ILanguageClient,
    ILanguageClientCustomMessage2,
    IDisposable
{
    private static readonly IReadOnlyDictionary<string, bool> ServerInitializationOptions =
        new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["semanticClassificationNotifications"] = true,
        };

    private readonly object serverProcessGate = new();
    private readonly ViuCompletionColorMiddleLayer middleLayer =
        new(ViuCompletionColorState.Shared);
    private readonly ViuLanguageClientCustomMessageTarget customMessageTarget =
        new(
            new ViuSemanticClassificationReceiver(
                ViuSemanticClassificationState.Shared));
    private Process? serverProcess;
    private string? activationFailureMessage;
    private bool disposed;

    /// <inheritdoc />
    public string Name => "Viu Language Server";

    /// <summary>
    /// Gets the configuration sections the server reads through <c>workspace/configuration</c>.
    /// </summary>
    /// <remarks>
    /// The server answers no configuration request and asks for none; its only settings input is the
    /// project's own CSS-first Viu Utilities entry, which it reads from disk.
    /// </remarks>
    public IEnumerable<string>? ConfigurationSections => null;

    /// <summary>
    /// Gets the value sent as the <c>initialize</c> request's <c>initializationOptions</c>.
    /// </summary>
    /// <remarks>
    /// The option enables Viu's exact-classification notification. It deliberately carries no
    /// project or workspace state; documents are still placed in projects from their file paths.
    /// </remarks>
    public object InitializationOptions => ServerInitializationOptions;

    /// <inheritdoc />
    public object MiddleLayer => this.middleLayer;

    /// <inheritdoc />
    public object CustomMessageTarget => this.customMessageTarget;

    /// <summary>
    /// Gets the glob patterns Visual Studio watches on the server's behalf.
    /// </summary>
    /// <remarks>
    /// None: the server handles no <c>workspace/didChangeWatchedFiles</c> notification. It deliberately
    /// runs without file watchers and re-validates each cached project cone by a cheap stat on the next
    /// request, so a watch feed would be discarded on arrival.
    /// </remarks>
    public IEnumerable<string>? FilesToWatch => null;

    /// <summary>
    /// Gets a value indicating that a failed initialization is surfaced to the user.
    /// </summary>
    /// <remarks>
    /// A language server that never starts is otherwise indistinguishable from one with nothing to
    /// say: colorization keeps working, and completion and diagnostics simply never appear. The
    /// notification, whose text comes from <see cref="OnServerInitializeFailedAsync"/>, is what makes
    /// a missing or crashed payload visible instead of silent.
    /// </remarks>
    public bool ShowNotificationOnInitializeFailed => true;

    /// <summary>Raised to ask Visual Studio to start this client.</summary>
    public event AsyncEventHandler<EventArgs>? StartAsync;

    // Raised to ask Visual Studio to stop this client. Viu never asks: a server that dies takes its
    // standard output stream with it, and Visual Studio subscribes to that disconnection itself
    // (InitializationStatus.SubscribingToDisconnectionEvents). Declaring the event is the interface
    // contract; raising it as well would race that subscription with no behavior to gain.
#pragma warning disable CS0067
    /// <summary>Raised to ask Visual Studio to stop this client.</summary>
    public event AsyncEventHandler<EventArgs>? StopAsync;
#pragma warning restore CS0067

    /// <summary>
    /// Starts the packaged language server and returns the connection Visual Studio drives the
    /// protocol over.
    /// </summary>
    /// <param name="token">Cancels the activation.</param>
    /// <returns>
    /// The server's standard output and standard input as a reader/writer pair, or
    /// <see langword="null"/> when the payload could not be started — in which case
    /// <see cref="OnServerInitializeFailedAsync"/> reports why.
    /// </returns>
    public Task<Connection?> ActivateAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Volatile.Write(ref this.activationFailureMessage, null);
        ViuSemanticClassificationState.Shared.ClearPublications();
        ViuCompletionColorState.Shared.Clear();

        string extensionDirectory = ViuLanguageServerConfiguration.GetExtensionDirectory(
            typeof(ViuLanguageClient).Assembly.Location);
        ViuLanguageServerConfiguration configuration;
        string executablePath;

        try
        {
            configuration = ViuLanguageServerConfiguration.Load(extensionDirectory);
            executablePath = configuration.ResolveExecutablePath(
                extensionDirectory,
                RuntimeInformation.ProcessArchitecture);
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidDataException or IOException or
                UnauthorizedAccessException or PlatformNotSupportedException or ArgumentException)
        {
            // A malformed language-server.json, a configured path escaping the extension directory,
            // an unreadable extension directory, and a host architecture with no packaged server all
            // report the same way: a named reason on the initialization notification, never an opaque
            // throw out of activation. InvalidDataException is listed on its own because it derives
            // from SystemException, not IOException.
            return this.FailActivationAsync(
                $"The Viu language server could not be located under '{extensionDirectory}'. {exception.Message}");
        }

        if (!File.Exists(executablePath))
        {
            return this.FailActivationAsync(
                $"The Viu language server executable was not found at '{executablePath}'. " +
                "Reinstall the Viu extension, or rebuild it so the language-server payload is packaged.");
        }

        Process process = new()
        {
            StartInfo = configuration.CreateProcessStartInformation(
                executablePath,
                extensionDirectory),
        };

        try
        {
            if (!process.Start())
            {
                // Documented as "a process was reused"; there is no server to talk to either way.
                TerminateServerProcess(process);
                return this.FailActivationAsync(
                    $"The Viu language server at '{executablePath}' did not start.");
            }
        }
        catch (Exception exception) when (
            exception is Win32Exception or InvalidOperationException or
                PlatformNotSupportedException)
        {
            TerminateServerProcess(process);
            return this.FailActivationAsync(
                $"The Viu language server at '{executablePath}' could not be started. {exception.Message}");
        }

        // Read before the exit hook is armed. Releasing the process handle nulls the redirected
        // reader and writer accessors, so a server that dies this instant would otherwise turn the
        // two property reads below into an exception thrown out of activation.
        Stream reader = process.StandardOutput.BaseStream;
        Stream writer = process.StandardInput.BaseStream;

        lock (this.serverProcessGate)
        {
            if (this.disposed)
            {
                TerminateServerProcess(process);
                return this.FailActivationAsync("The Viu language client was disposed while activating.");
            }

            // A re-activation replaces the previous session outright; two servers must never share
            // one client.
            this.TerminateCurrentServerProcess();
            this.serverProcess = process;
        }

        // Releasing the handle as soon as the server exits is all the exit hook owes: the server's own
        // shutdown needs no help. Visual Studio disposes the Connection when the session ends, which
        // closes the standard-input pipe, and the server's message loop returns on that end of file.
        // The same end of file is what stops an orphan if devenv.exe dies without disposing anything.
        // Subscribing before enabling keeps an already-exited server from slipping past the handler.
        process.Exited += this.OnServerProcessExited;
        process.EnableRaisingEvents = true;

        return Task.FromResult<Connection?>(new Connection(reader, writer));
    }

    /// <inheritdoc />
    public Task OnLoadedAsync() => this.StartAsync.InvokeAsync(this, EventArgs.Empty);

    /// <inheritdoc />
    public Task OnServerInitializedAsync() => Task.CompletedTask;

    /// <inheritdoc />
    /// <remarks>
    /// The connection is retained so the Quick Info adapter can ask the server for hover content
    /// directly. It renders the tooltip from classified runs rather than handing Visual Studio the
    /// server's Markdown, and the middle layer therefore declines the editor's own hover request —
    /// two answers to one gesture would stack two tooltips.
    /// </remarks>
    public Task AttachForCustomMessageAsync(JsonRpc rpc)
    {
        ViuLanguageServerConnection.Attach(rpc);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Composes the message shown when the server never reached an initialized state.
    /// </summary>
    /// <param name="initializationState">Visual Studio's account of how far initialization got.</param>
    /// <returns>The failure context carrying a message naming an action the user can take.</returns>
    public Task<InitializationFailureContext?> OnServerInitializeFailedAsync(
        ILanguageClientInitializationInfo initializationState)
    {
        string? reason = Volatile.Read(ref this.activationFailureMessage) ??
            initializationState?.InitializationException?.Message ??
            initializationState?.StatusMessage;

        return Task.FromResult<InitializationFailureContext?>(
            new InitializationFailureContext
            {
                FailureMessage = string.IsNullOrWhiteSpace(reason)
                    ? "Viu language features are unavailable: the Viu language server did not start."
                    : "Viu language features are unavailable: " + reason,
            });
    }

    /// <summary>
    /// Terminates the language server the client still owns.
    /// </summary>
    /// <remarks>
    /// The backstop, not the ordinary path — Visual Studio disposes the composition container at
    /// shutdown, by which point the connection is normally already closed and the server already gone.
    /// A server still running at that point is killed rather than waited on: it holds no unflushed
    /// state, every response is written as it is produced, and blocking the shutdown path to be polite
    /// to a process about to lose its pipes buys nothing.
    /// </remarks>
    public void Dispose()
    {
        lock (this.serverProcessGate)
        {
            this.disposed = true;
            this.TerminateCurrentServerProcess();
        }

        ViuSemanticClassificationState.Shared.ClearPublications();
        ViuCompletionColorState.Shared.Clear();
    }

    private void OnServerProcessExited(object sender, EventArgs arguments)
    {
        if (sender is not Process process)
        {
            return;
        }

        lock (this.serverProcessGate)
        {
            if (!ReferenceEquals(this.serverProcess, process))
            {
                // Already replaced or already terminated; that owner disposes it.
                return;
            }

            this.serverProcess = null;
        }

        process.Exited -= this.OnServerProcessExited;

        // Process.Close deliberately leaves the redirected reader and writer alone, so releasing the
        // handle here cannot pull the streams out from under a connection Visual Studio still holds.
        process.Dispose();
        ViuSemanticClassificationState.Shared.ClearPublications();
        ViuCompletionColorState.Shared.Clear();
    }

    // Callers hold serverProcessGate.
    private void TerminateCurrentServerProcess()
    {
        Process? process = this.serverProcess;
        if (process is null)
        {
            return;
        }

        this.serverProcess = null;
        process.Exited -= this.OnServerProcessExited;
        TerminateServerProcess(process);
    }

    private static void TerminateServerProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Never started, already gone, or exiting between the test and the kill.
        }
        finally
        {
            process.Dispose();
        }
    }

    private Task<Connection?> FailActivationAsync(string message)
    {
        Volatile.Write(ref this.activationFailureMessage, message);
        return Task.FromResult<Connection?>(null);
    }
}
