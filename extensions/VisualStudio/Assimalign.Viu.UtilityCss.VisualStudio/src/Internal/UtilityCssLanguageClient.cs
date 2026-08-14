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

namespace Assimalign.Viu.UtilityCss.VisualStudio;

/// <summary>
/// Connects Visual Studio's standard LSP presentation to the standalone Viu Utilities language
/// server over standard input and output.
/// </summary>
/// <remarks>
/// The export attaches only to Visual Studio-owned top-level HTML content types. It contributes no
/// grammar, content type, classifier, completion manager, hover adapter, or document-color adapter;
/// all presentation remains the editor's standard Language Server Protocol behavior.
/// </remarks>
[Export(typeof(ILanguageClient))]
[ContentType(UtilityCssContentTypes.HtmlDelegation)]
internal sealed class UtilityCssLanguageClient : ILanguageClient, IDisposable
{
    private static readonly object ServerInitializationOptions = new();

    private readonly object serverProcessGate = new();
    private Process? serverProcess;
    private string? activationFailureMessage;
    private bool disposed;

    /// <inheritdoc />
    public string Name => "Viu Utilities Language Server";

    /// <inheritdoc />
    public IEnumerable<string>? ConfigurationSections => null;

    /// <inheritdoc />
    public object InitializationOptions => ServerInitializationOptions;

    /// <inheritdoc />
    public IEnumerable<string>? FilesToWatch => null;

    /// <inheritdoc />
    public bool ShowNotificationOnInitializeFailed => true;

    /// <inheritdoc />
    public event AsyncEventHandler<EventArgs>? StartAsync;

#pragma warning disable CS0067
    /// <inheritdoc />
    public event AsyncEventHandler<EventArgs>? StopAsync;
#pragma warning restore CS0067

    /// <inheritdoc />
    public Task<Connection?> ActivateAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Volatile.Write(ref this.activationFailureMessage, null);

        string extensionDirectory = UtilityCssLanguageServerConfiguration.GetExtensionDirectory(
            typeof(UtilityCssLanguageClient).Assembly.Location);
        UtilityCssLanguageServerConfiguration configuration;
        string executablePath;

        try
        {
            configuration = UtilityCssLanguageServerConfiguration.Load(extensionDirectory);
            executablePath = configuration.ResolveExecutablePath(
                extensionDirectory,
                RuntimeInformation.ProcessArchitecture);
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidDataException or IOException or
                UnauthorizedAccessException or PlatformNotSupportedException or ArgumentException)
        {
            return this.FailActivationAsync(
                $"The Viu Utilities language server could not be located under '{extensionDirectory}'. {exception.Message}");
        }

        if (!File.Exists(executablePath))
        {
            return this.FailActivationAsync(
                $"The Viu Utilities language server executable was not found at '{executablePath}'. " +
                "Reinstall the extension, or rebuild it so both architecture payloads are packaged.");
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
                TerminateServerProcess(process);
                return this.FailActivationAsync(
                    $"The Viu Utilities language server at '{executablePath}' did not start.");
            }
        }
        catch (Exception exception) when (
            exception is Win32Exception or InvalidOperationException or
                PlatformNotSupportedException)
        {
            TerminateServerProcess(process);
            return this.FailActivationAsync(
                $"The Viu Utilities language server at '{executablePath}' could not be started. {exception.Message}");
        }

        Stream reader = process.StandardOutput.BaseStream;
        Stream writer = process.StandardInput.BaseStream;

        lock (this.serverProcessGate)
        {
            if (this.disposed)
            {
                TerminateServerProcess(process);
                return this.FailActivationAsync(
                    "The Viu Utilities language client was disposed while activating.");
            }

            this.TerminateCurrentServerProcess();
            this.serverProcess = process;
        }

        process.Exited += this.OnServerProcessExited;
        process.EnableRaisingEvents = true;

        return Task.FromResult<Connection?>(new Connection(reader, writer));
    }

    /// <inheritdoc />
    public Task OnLoadedAsync() => this.StartAsync.InvokeAsync(this, EventArgs.Empty);

    /// <inheritdoc />
    public Task OnServerInitializedAsync() => Task.CompletedTask;

    /// <inheritdoc />
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
                    ? "Viu Utilities language features are unavailable: the language server did not start."
                    : "Viu Utilities language features are unavailable: " + reason,
            });
    }

    /// <summary>Terminates a language server still owned when the MEF part is disposed.</summary>
    public void Dispose()
    {
        lock (this.serverProcessGate)
        {
            this.disposed = true;
            this.TerminateCurrentServerProcess();
        }
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
                return;
            }

            this.serverProcess = null;
        }

        process.Exited -= this.OnServerProcessExited;
        process.Dispose();
    }

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
            // The process never started, already exited, or exited between the check and kill.
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
