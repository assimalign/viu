using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Assimalign.Viu.Sdk.CssHotReload;

internal static class Program
{
    public static int Main(string[] arguments)
    {
        if (!CssHotReloadOptions.TryParse(
                arguments,
                Console.Error,
                out var options))
        {
            return 2;
        }

        var ownerProcessIdentifier = options.OwnerProcessIdentifier;
        var parentProcessIdentifier = 0;
        if (ownerProcessIdentifier is null &&
            (!ProcessLifetime.TryGetParentProcessIdentifier(
                options.LauncherProcessIdentifier!.Value,
                out parentProcessIdentifier)))
        {
            Console.Error.WriteLine(
                "Viu Generated Asset Hot Reload could not identify the dotnet-watch owner process.");
            return 3;
        }
        else if (ownerProcessIdentifier is null)
        {
            ownerProcessIdentifier = parentProcessIdentifier;
        }

        if (!ProcessLifetime.TryCapture(
                ownerProcessIdentifier.Value,
                out var ownerIdentity))
        {
            Console.Error.WriteLine(
                "Viu Generated Asset Hot Reload owner process is no longer running.");
            return 3;
        }

        var mutexName = CreateMutexName(options.ProjectPath);
        using var mutex = new Mutex(initiallyOwned: false, mutexName);
        var ownsMutex = false;
        try
        {
            try
            {
                ownsMutex = mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                ownsMutex = true;
            }

            if (!ownsMutex)
            {
                return 0;
            }

            using var cancellation = new CancellationTokenSource();
            ConsoleCancelEventHandler cancelHandler = (_, eventArguments) =>
            {
                eventArguments.Cancel = true;
                cancellation.Cancel();
            };
            Console.CancelKeyPress += cancelHandler;
            EventHandler processExitHandler = (_, _) => cancellation.Cancel();
            AppDomain.CurrentDomain.ProcessExit += processExitHandler;
            try
            {
                var worker = new CssHotReloadWorker(options, ownerIdentity);
                worker.RunAsync(cancellation.Token).GetAwaiter().GetResult();
                return 0;
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch (Exception exception) when (
                exception is IOException or
                    UnauthorizedAccessException or
                    InvalidOperationException)
            {
                Console.Error.WriteLine(
                    "Viu Generated Asset Hot Reload stopped: " + exception.Message);
                return 4;
            }
            finally
            {
                AppDomain.CurrentDomain.ProcessExit -= processExitHandler;
                Console.CancelKeyPress -= cancelHandler;
            }
        }
        finally
        {
            if (ownsMutex)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    private static string CreateMutexName(string projectPath)
    {
        var normalizedPath = OperatingSystem.IsWindows()
            ? projectPath.ToUpperInvariant()
            : projectPath;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath));
        return "Assimalign.Viu.CssHotReload." + Convert.ToHexString(bytes);
    }
}
