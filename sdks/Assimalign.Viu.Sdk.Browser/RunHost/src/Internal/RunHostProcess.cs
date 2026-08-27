using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.Sdk.Browser.RunHost;

internal static class RunHostProcess
{
    private const string ArgumentSeparator = "--";
    private const string BrowserRefreshEndpointEnvironmentVariable =
        "ASPNETCORE_AUTO_RELOAD_WS_ENDPOINT";
    private const string GeneratedAssetWorkerAssemblyArgument =
        "--generated-asset-worker-assembly";
    private const string GeneratedAssetWorkerConfigurationArgument =
        "--generated-asset-worker-configuration";
    private const string GeneratedAssetWorkerStateArgument =
        "--generated-asset-worker-state";
    private const string DefaultReadinessPrefix = "App url:";
    private const string DotNetWatchReadinessPrefix = "Now listening on:";
    private const string ReadinessPrefixArgument = "--readiness-prefix";
    private static readonly TimeSpan GracefulTerminationTimeout = TimeSpan.FromSeconds(3);

    internal static async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        if (!TryParseInvocation(arguments, out RunHostInvocation? invocation)
            || invocation is null)
        {
            await standardError.WriteLineAsync(
                "Usage: Assimalign.Viu.Sdk.Browser.RunHost "
                + "[--readiness-prefix <text>] "
                + "[--generated-asset-worker-assembly <path> "
                + "--generated-asset-worker-configuration <path> "
                + "--generated-asset-worker-state <path>] "
                + "-- <command> [arguments]");
            return 2;
        }

        ProcessStartInfo startInformation = new()
        {
            FileName = invocation.Command,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in invocation.CommandArguments)
        {
            startInformation.ArgumentList.Add(argument);
        }

        BrowserRefreshBridge? browserRefreshBridge = null;
        string? browserRefreshEndpoints = Environment.GetEnvironmentVariable(
            BrowserRefreshEndpointEnvironmentVariable);
        if (invocation.GeneratedAssetWorkerAssemblyPath is not null
            && invocation.GeneratedAssetWorkerConfigurationFilePath is not null
            && invocation.GeneratedAssetWorkerStateFilePath is not null
            && File.Exists(invocation.GeneratedAssetWorkerConfigurationFilePath)
            && !GeneratedAssetWorkerHost.IsWorkerActive(
                invocation.GeneratedAssetWorkerStateFilePath)
            && !string.IsNullOrWhiteSpace(browserRefreshEndpoints))
        {
            try
            {
                IReadOnlyList<string> managedStylesheetPaths =
                    GeneratedAssetWorkerHost.ReadManagedStylesheetPaths(
                        invocation.GeneratedAssetWorkerConfigurationFilePath);
                browserRefreshBridge = await BrowserRefreshBridge.StartAsync(
                    browserRefreshEndpoints,
                    managedStylesheetPaths,
                    (connectionIdentifier, messageType, path) =>
                        ReportUpstreamBrowserRefreshMessage(
                            standardOutput,
                            connectionIdentifier,
                            messageType,
                            path),
                    CancellationToken.None);
                startInformation.Environment[BrowserRefreshEndpointEnvironmentVariable] =
                    browserRefreshBridge.ChildEndpointList;
                _ = GeneratedAssetWorkerHost.TryStart(
                    invocation.GeneratedAssetWorkerAssemblyPath,
                    invocation.GeneratedAssetWorkerConfigurationFilePath,
                    browserRefreshBridge.SendUpdateStaticFileAsync,
                    standardOutput,
                    standardError);
            }
            catch (Exception exception) when (
                exception is ArgumentException or
                    InvalidDataException or
                    IOException or
                    InvalidOperationException or
                    NotSupportedException or
                    System.Net.Sockets.SocketException or
                    UnauthorizedAccessException)
            {
                startInformation.Environment[BrowserRefreshEndpointEnvironmentVariable] =
                    browserRefreshEndpoints;
                if (browserRefreshBridge is not null)
                {
                    try
                    {
                        await browserRefreshBridge.DisposeAsync();
                    }
                    catch (Exception disposalException) when (
                        disposalException is IOException or
                            InvalidOperationException or
                            ObjectDisposedException)
                    {
                        await standardError.WriteLineAsync(
                            "Viu Generated Asset Hot Reload could not fully dispose its failed "
                            + "BrowserRefresh bridge: "
                            + disposalException.Message);
                    }
                }

                await standardError.WriteLineAsync(
                    "Viu Generated Asset Hot Reload could not start its BrowserRefresh bridge; "
                    + "the application will use the original host refresh endpoint. "
                    + exception.Message);
                browserRefreshBridge = null;
            }
        }

        try
        {
            using Process process = new() { StartInfo = startInformation };
            if (!process.Start())
            {
                await standardError.WriteLineAsync(
                    $"Could not start the Browser application run command '{startInformation.FileName}'.");
                return 1;
            }

            int cancellationCount = 0;
            ConsoleCancelEventHandler cancellationHandler = (_, eventArguments) =>
            {
                eventArguments.Cancel = true;
                if (Interlocked.Increment(ref cancellationCount) == 1)
                {
                    ScheduleForcedTermination(process);
                }
                else
                {
                    TryTerminateProcessTree(process);
                }
            };
            EventHandler processExitHandler = (_, _) => TryTerminateProcessTree(process);
            PosixSignalRegistration? terminationRegistration = null;

            Console.CancelKeyPress += cancellationHandler;
            AppDomain.CurrentDomain.ProcessExit += processExitHandler;
            if (!OperatingSystem.IsWindows())
            {
                terminationRegistration = PosixSignalRegistration.Create(
                    PosixSignal.SIGTERM,
                    context =>
                    {
                        context.Cancel = true;
                        TrySendTerminationSignal(process);
                        ScheduleForcedTermination(process);
                    });
            }

            try
            {
                Task standardOutputTask = ForwardLinesAsync(
                    process.StandardOutput,
                    standardOutput,
                    invocation.ReadinessPrefix);
                Task standardErrorTask = ForwardLinesAsync(
                    process.StandardError,
                    standardError,
                    invocation.ReadinessPrefix);

                await process.WaitForExitAsync();
                await Task.WhenAll(standardOutputTask, standardErrorTask);
                return process.ExitCode;
            }
            finally
            {
                terminationRegistration?.Dispose();
                AppDomain.CurrentDomain.ProcessExit -= processExitHandler;
                Console.CancelKeyPress -= cancellationHandler;
                TryTerminateProcessTree(process);
            }
        }
        finally
        {
            if (browserRefreshBridge is not null)
            {
                await browserRefreshBridge.DisposeAsync();
            }
        }
    }

    private static bool TryParseInvocation(
        IReadOnlyList<string> arguments,
        out RunHostInvocation? invocation)
    {
        string? workerAssemblyPath = null;
        string? workerConfigurationFilePath = null;
        string? workerStateFilePath = null;
        string readinessPrefix = DefaultReadinessPrefix;
        int index = 0;
        while (index < arguments.Count
            && !string.Equals(
                arguments[index],
                ArgumentSeparator,
                StringComparison.Ordinal))
        {
            string argument = arguments[index];
            if (index + 1 >= arguments.Count
                || string.IsNullOrWhiteSpace(arguments[index + 1]))
            {
                invocation = null;
                return false;
            }

            if (string.Equals(
                    argument,
                    GeneratedAssetWorkerAssemblyArgument,
                    StringComparison.Ordinal))
            {
                workerAssemblyPath = arguments[index + 1];
            }
            else if (string.Equals(
                    argument,
                    GeneratedAssetWorkerConfigurationArgument,
                    StringComparison.Ordinal))
            {
                workerConfigurationFilePath = arguments[index + 1];
            }
            else if (string.Equals(
                    argument,
                    GeneratedAssetWorkerStateArgument,
                    StringComparison.Ordinal))
            {
                workerStateFilePath = arguments[index + 1];
            }
            else if (string.Equals(
                    argument,
                    ReadinessPrefixArgument,
                    StringComparison.Ordinal))
            {
                readinessPrefix = arguments[index + 1];
            }
            else
            {
                invocation = null;
                return false;
            }

            index += 2;
        }

        if (index + 1 >= arguments.Count
            || !string.Equals(
                arguments[index],
                ArgumentSeparator,
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(arguments[index + 1])
            || (workerAssemblyPath is null) != (workerConfigurationFilePath is null)
            || (workerAssemblyPath is null) != (workerStateFilePath is null))
        {
            invocation = null;
            return false;
        }

        List<string> commandArguments = [];
        for (int argumentIndex = index + 2;
            argumentIndex < arguments.Count;
            argumentIndex++)
        {
            commandArguments.Add(arguments[argumentIndex]);
        }

        invocation = new RunHostInvocation(
            arguments[index + 1],
            commandArguments,
            readinessPrefix,
            workerAssemblyPath,
            workerConfigurationFilePath,
            workerStateFilePath);
        return true;
    }

    private static async Task ForwardLinesAsync(
        StreamReader reader,
        TextWriter writer,
        string? readinessPrefix)
    {
        while (await reader.ReadLineAsync() is string line)
        {
            await writer.WriteLineAsync(line);
            if (readinessPrefix is not null
                && !StartsWithReadinessPrefix(line, DotNetWatchReadinessPrefix)
                && TryReadReadinessAddress(
                    line,
                    readinessPrefix,
                    out string? address))
            {
                // [V01.01.12.31], #349: dotnet-watch 10.0.302 recognizes this readiness
                // marker but not the configurable equivalent marker mirrored here.
                await writer.WriteLineAsync($"{DotNetWatchReadinessPrefix} {address}");
            }
        }

        await writer.FlushAsync();
    }

    private static void ReportUpstreamBrowserRefreshMessage(
        TextWriter writer,
        long connectionIdentifier,
        string messageType,
        string? path)
    {
        try
        {
            string pathDetail = path is null ? string.Empty : $"; path={path}";
            writer.WriteLine(
                "Viu BrowserRefresh bridge upstream: "
                + $"connection={connectionIdentifier}; type={messageType}{pathDetail}.");
        }
        catch (Exception exception) when (
            exception is IOException or ObjectDisposedException)
        {
        }
    }

    private static bool StartsWithReadinessPrefix(
        string line,
        string readinessPrefix) =>
        line.AsSpan().TrimStart().StartsWith(
            readinessPrefix,
            StringComparison.Ordinal);

    private static bool TryReadReadinessAddress(
        string line,
        string readinessPrefix,
        out string? address)
    {
        ReadOnlySpan<char> content = line.AsSpan().TrimStart();
        if (!content.StartsWith(readinessPrefix, StringComparison.Ordinal))
        {
            address = null;
            return false;
        }

        string candidate = content[readinessPrefix.Length..]
            .Trim()
            .ToString();
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? addressUri)
            || (addressUri.Scheme != Uri.UriSchemeHttp
                && addressUri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(addressUri.Host))
        {
            address = null;
            return false;
        }

        address = candidate;
        return true;
    }

    private static void TrySendTerminationSignal(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                _ = NativeProcessSignals.Send(
                    process.Id,
                    NativeProcessSignals.Termination);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void ScheduleForcedTermination(Process process)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(GracefulTerminationTimeout);
            TryTerminateProcessTree(process);
        });
    }

    private static void TryTerminateProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    private sealed record RunHostInvocation(
        string Command,
        IReadOnlyList<string> CommandArguments,
        string ReadinessPrefix,
        string? GeneratedAssetWorkerAssemblyPath,
        string? GeneratedAssetWorkerConfigurationFilePath,
        string? GeneratedAssetWorkerStateFilePath);
}
