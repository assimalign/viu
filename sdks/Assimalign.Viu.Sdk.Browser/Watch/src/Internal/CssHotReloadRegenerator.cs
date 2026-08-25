using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.Sdk.CssHotReload;

internal sealed class CssHotReloadRegenerator
{
    private readonly CssHotReloadOptions options;
    private readonly CssHotReloadEventLog eventLog;
    private readonly string regenerationTargets;

    public CssHotReloadRegenerator(
        CssHotReloadOptions options,
        CssHotReloadEventLog eventLog)
    {
        this.options = options;
        this.eventLog = eventLog;
        regenerationTargets = string.Join(
            ";",
            options.GeneratedAssets
                .Select(asset => asset.RegenerationTarget)
                .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    public async Task<bool> RegenerateAsync(CancellationToken cancellationToken)
    {
        eventLog.Append("start");
        eventLog.Append("targets:" + regenerationTargets);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var startInfo = CreateProcessStartInfo();
            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                eventLog.Append("complete:failed-to-start");
                return false;
            }

            var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None);
                }

                throw;
            }

            var standardOutput = await standardOutputTask;
            var standardError = await standardErrorTask;
            if (process.ExitCode == 0)
            {
                eventLog.Append("complete:0");
                return true;
            }

            if (attempt == 0)
            {
                await Task.Delay(100, cancellationToken);
                continue;
            }

            Console.Error.WriteLine(
                "Viu Generated Asset Hot Reload regeneration failed with exit code " +
                process.ExitCode.ToString(CultureInfo.InvariantCulture) +
                "." +
                Environment.NewLine +
                standardOutput +
                Environment.NewLine +
                standardError);
            eventLog.Append(
                "complete:" +
                process.ExitCode.ToString(CultureInfo.InvariantCulture));
        }

        return false;
    }

    private ProcessStartInfo CreateProcessStartInfo()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = options.DotNetHostPath,
            WorkingDirectory = options.ProjectDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("msbuild");
        startInfo.ArgumentList.Add(options.ProjectPath);
        startInfo.ArgumentList.Add("-nologo");
        startInfo.ArgumentList.Add("-verbosity:quiet");
        startInfo.ArgumentList.Add("-target:" + regenerationTargets);
        startInfo.ArgumentList.Add("-property:ViuGeneratedAssetHotReload=true");
        startInfo.ArgumentList.Add("-property:DotNetWatchBuild=false");
        startInfo.ArgumentList.Add("-property:DesignTimeBuild=false");
        startInfo.ArgumentList.Add("-property:BuildProjectReferences=false");
        startInfo.ArgumentList.Add(
            "-property:Configuration=" + options.Configuration);
        if (!string.IsNullOrEmpty(options.TargetFramework))
        {
            startInfo.ArgumentList.Add(
                "-property:TargetFramework=" + options.TargetFramework);
        }

        if (!string.IsNullOrEmpty(options.RuntimeIdentifier))
        {
            startInfo.ArgumentList.Add(
                "-property:RuntimeIdentifier=" + options.RuntimeIdentifier);
        }

        startInfo.Environment["VIU_GENERATED_ASSET_HOT_RELOAD"] = "1";
        return startInfo;
    }
}
