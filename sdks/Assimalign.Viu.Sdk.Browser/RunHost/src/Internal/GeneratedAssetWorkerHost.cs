using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.Sdk.Browser.RunHost;

internal sealed class GeneratedAssetWorkerHost
{
    private const string UpdateMarker = "viu-generated-asset-update:";

    private readonly Process process;

    private GeneratedAssetWorkerHost(Process process)
    {
        this.process = process;
    }

    internal static bool IsWorkerActive(string stateFilePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(stateFilePath);

        try
        {
            if (!File.Exists(stateFilePath))
            {
                return false;
            }

            int processIdentifier = 0;
            long processStartTicks = 0;
            foreach (string line in File.ReadAllLines(stateFilePath))
            {
                if (line.StartsWith("worker=", StringComparison.Ordinal))
                {
                    int.TryParse(
                        line.Substring("worker=".Length),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out processIdentifier);
                }
                else if (line.StartsWith("worker-start=", StringComparison.Ordinal))
                {
                    long.TryParse(
                        line.Substring("worker-start=".Length),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out processStartTicks);
                }
            }

            if (processIdentifier <= 0 || processStartTicks <= 0)
            {
                return false;
            }

            using Process workerProcess = Process.GetProcessById(processIdentifier);
            return !workerProcess.HasExited
                && workerProcess.StartTime.ToUniversalTime().Ticks == processStartTicks;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
                InvalidOperationException or
                IOException or
                UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal static GeneratedAssetWorkerHost? TryStart(
        string workerAssemblyPath,
        string configurationFilePath,
        Func<string, CancellationToken, ValueTask> updateStaticFile,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        ArgumentException.ThrowIfNullOrEmpty(workerAssemblyPath);
        ArgumentException.ThrowIfNullOrEmpty(configurationFilePath);
        ArgumentNullException.ThrowIfNull(updateStaticFile);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        if (!File.Exists(workerAssemblyPath))
        {
            standardError.WriteLine(
                $"Viu Generated Asset Hot Reload worker assembly was not found at '{workerAssemblyPath}'.");
            return null;
        }

        if (!File.Exists(configurationFilePath))
        {
            return null;
        }

        string dotNetHostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")
            ?? Environment.ProcessPath
            ?? "dotnet";
        ProcessStartInfo startInformation = new()
        {
            FileName = dotNetHostPath,
            WorkingDirectory = Environment.CurrentDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInformation.ArgumentList.Add("exec");
        startInformation.ArgumentList.Add(Path.GetFullPath(workerAssemblyPath));
        startInformation.ArgumentList.Add("--configuration-file");
        startInformation.ArgumentList.Add(Path.GetFullPath(configurationFilePath));
        startInformation.ArgumentList.Add("--owner-process-identifier");
        startInformation.ArgumentList.Add(
            Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        startInformation.ArgumentList.Add("--report-updates");
        startInformation.Environment["VIU_GENERATED_ASSET_HOT_RELOAD"] = "1";

        try
        {
            Process workerProcess = new() { StartInfo = startInformation };
            if (!workerProcess.Start())
            {
                workerProcess.Dispose();
                standardError.WriteLine(
                    "Viu Generated Asset Hot Reload worker could not be started by the Browser run host.");
                return null;
            }

            GeneratedAssetWorkerHost host = new(workerProcess);
            host.ForwardOutput(updateStaticFile, standardOutput, standardError);
            return host;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
                System.ComponentModel.Win32Exception or
                IOException or
                UnauthorizedAccessException)
        {
            standardError.WriteLine(
                "Viu Generated Asset Hot Reload worker could not be started by the Browser run host: "
                + exception.Message);
            return null;
        }
    }

    private void ForwardOutput(
        Func<string, CancellationToken, ValueTask> updateStaticFile,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                while (await process.StandardOutput.ReadLineAsync() is string line)
                {
                    if (TryDecodeUpdate(line, out string? path))
                    {
                        await updateStaticFile(
                            NormalizeClientPath(path),
                            CancellationToken.None);
                    }
                    else
                    {
                        await standardOutput.WriteLineAsync(line);
                    }
                }
            }
            catch (Exception exception) when (
                exception is IOException or
                    InvalidOperationException or
                    ObjectDisposedException)
            {
                await standardError.WriteLineAsync(
                    "Viu Generated Asset Hot Reload update forwarding stopped: "
                    + exception.Message);
            }
        });

        _ = Task.Run(async () =>
        {
            try
            {
                while (await process.StandardError.ReadLineAsync() is string line)
                {
                    await standardError.WriteLineAsync(line);
                }
            }
            catch (Exception exception) when (
                exception is IOException or
                    InvalidOperationException or
                    ObjectDisposedException)
            {
                await standardError.WriteLineAsync(
                    "Viu Generated Asset Hot Reload diagnostic forwarding stopped: "
                    + exception.Message);
            }
        });
    }

    private static bool TryDecodeUpdate(string line, out string path)
    {
        path = string.Empty;
        if (!line.StartsWith(UpdateMarker, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            path = Encoding.UTF8.GetString(
                Convert.FromBase64String(line.Substring(UpdateMarker.Length)));
            return !string.IsNullOrWhiteSpace(path);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string NormalizeClientPath(string staticWebAssetPath)
    {
        string path = staticWebAssetPath.Replace('\\', '/');
        const string webRootPrefix = "wwwroot/";
        if (path.StartsWith(webRootPrefix, StringComparison.Ordinal))
        {
            path = path.Substring(webRootPrefix.Length);
        }

        return "/" + path.TrimStart('/');
    }
}
