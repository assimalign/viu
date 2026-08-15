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
    private const string WasmAppHostAddressPrefix = "App url:";
    private static readonly TimeSpan GracefulTerminationTimeout = TimeSpan.FromSeconds(3);

    internal static async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        if (arguments.Count < 2
            || !string.Equals(arguments[0], ArgumentSeparator, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(arguments[1]))
        {
            await standardError.WriteLineAsync(
                "Usage: Assimalign.Viu.Sdk.Browser.RunHost -- <command> [arguments]");
            return 2;
        }

        ProcessStartInfo startInformation = new()
        {
            FileName = arguments[1],
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        for (int index = 2; index < arguments.Count; index++)
        {
            startInformation.ArgumentList.Add(arguments[index]);
        }

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
                standardOutput);
            Task standardErrorTask = ForwardLinesAsync(
                process.StandardError,
                standardError);

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

    private static async Task ForwardLinesAsync(
        StreamReader reader,
        TextWriter writer)
    {
        while (await reader.ReadLineAsync() is string line)
        {
            await writer.WriteLineAsync(line);
            if (TryReadWasmAppHostAddress(line, out string? address))
            {
                // [V01.01.12.31], #349: dotnet-watch 10.0.302 recognizes this readiness
                // marker but not the equivalent marker emitted by WasmAppHost.
                await writer.WriteLineAsync($"Now listening on: {address}");
            }
        }

        await writer.FlushAsync();
    }

    private static bool TryReadWasmAppHostAddress(
        string line,
        out string? address)
    {
        ReadOnlySpan<char> content = line.AsSpan().TrimStart();
        if (!content.StartsWith(WasmAppHostAddressPrefix, StringComparison.Ordinal))
        {
            address = null;
            return false;
        }

        string candidate = content[WasmAppHostAddressPrefix.Length..]
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
}
