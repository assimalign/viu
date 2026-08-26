using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Assimalign.Viu.Testing.EndToEnd;

internal sealed class VisualStudioHotReloadSession : IAsyncDisposable
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(3);
    private readonly string _artifactDirectory;
    private readonly ConcurrentQueue<string> _buildOutput = [];
    private readonly ConcurrentQueue<string> _runOutput = [];
    private Uri? _address;
    private Process? _process;
    private int? _workerProcessIdentifier;
    private bool _stopped;

    internal VisualStudioHotReloadSession(
        string projectPath,
        string viuVersion,
        string artifactDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectPath);
        ArgumentException.ThrowIfNullOrEmpty(viuVersion);
        ArgumentException.ThrowIfNullOrEmpty(artifactDirectory);
        ProjectPath = Path.GetFullPath(projectPath);
        ProjectDirectory = Path.GetDirectoryName(ProjectPath)
            ?? throw new ArgumentException(
                "The Visual Studio hot-reload project path has no parent directory.",
                nameof(projectPath));
        ViuVersion = viuVersion;
        _artifactDirectory = Path.GetFullPath(artifactDirectory);
        MainSourcePath = Path.Combine(ProjectDirectory, "HotReloadPage.vue");
        UtilityCandidateSourcePath = Path.Combine(
            ProjectDirectory,
            "UtilityCandidate.html");
        VisualStudioSourcePath = Path.Combine(
            ProjectDirectory,
            "VisualStudioGeneratedAssetProbe.viu");
        ComponentBundlePath = Path.Combine(
            ProjectDirectory,
            "obj",
            "Debug",
            "net10.0",
            "viu",
            "EndToEndHotReloadApp.viu.css");
        UtilityBundlePath = Path.Combine(
            ProjectDirectory,
            "obj",
            "Debug",
            "net10.0",
            "utilitycss",
            "EndToEndHotReloadApp.utilities.css");
        CssEventLogPath = Path.Combine(
            ProjectDirectory,
            "obj",
            "viu",
            "css-hot-reload",
            "events.log");
        WorkerStatePath = Path.Combine(
            ProjectDirectory,
            "obj",
            "viu",
            "css-hot-reload",
            "worker.state");
        WorkerConfigurationPath = WorkerStatePath + ".configuration";
    }

    internal Uri Address => _address
        ?? throw new InvalidOperationException(
            "The Visual Studio-shaped application has not reported its address.");

    internal string ProjectPath { get; }

    internal string ProjectDirectory { get; }

    internal string ViuVersion { get; }

    internal string MainSourcePath { get; }

    internal string UtilityCandidateSourcePath { get; }

    internal string VisualStudioSourcePath { get; }

    internal string ComponentBundlePath { get; }

    internal string UtilityBundlePath { get; }

    internal string CssEventLogPath { get; }

    internal string WorkerStatePath { get; }

    internal string WorkerConfigurationPath { get; }

    internal async Task StartAsync(
        VisualStudioBrowserRefreshStubServer refreshServer)
    {
        ArgumentNullException.ThrowIfNull(refreshServer);
        RequireFile(ProjectPath, "hot-reload fixture project");
        RequireFile(MainSourcePath, "mounted .vue source");
        RequireFile(UtilityCandidateSourcePath, "non-served utility candidate source");
        RequireFile(VisualStudioSourcePath, "Visual Studio generated-asset .viu source");
        RequireMissingFile(
            WorkerConfigurationPath,
            "ordinary-build generated-asset worker configuration");
        RequireMissingFile(WorkerStatePath, "generated-asset worker state");
        Directory.CreateDirectory(_artifactDirectory);

        await RunBuildAsync();
        RequireFile(
            WorkerConfigurationPath,
            "ordinary-build generated-asset worker configuration");
        RequireMissingFile(
            WorkerStatePath,
            "worker state after the ordinary Debug build");
        RequireFile(ComponentBundlePath, "initial component stylesheet bundle");
        RequireFile(UtilityBundlePath, "initial utility stylesheet bundle");

        string browserRefreshMiddlewareAssemblyPath =
            await ResolveBrowserRefreshMiddlewareAssemblyPathAsync();
        ProcessStartInfo startInformation = CreateDotNetStartInformation();
        foreach (string argument in new[]
        {
            "run",
            "--project",
            ProjectPath,
            "--configuration",
            "Debug",
            "--runtime",
            "browser-wasm",
            "--no-build",
            "--no-restore",
            "--launch-profile",
            "EndToEndHotReloadApp",
        })
        {
            startInformation.ArgumentList.Add(argument);
        }

        // [V01.01.12.30.05], #357: these are the public BrowserRefresh values Visual Studio
        // supplies to the packaged RunHost. The RunHost replaces them only for the application
        // child, leaving this upstream endpoint available to its bridge.
        startInformation.Environment["ASPNETCORE_AUTO_RELOAD_WS_ENDPOINT"] =
            refreshServer.Address.AbsoluteUri;
        startInformation.Environment["ASPNETCORE_AUTO_RELOAD_WS_KEY"] =
            refreshServer.PublicKey;
        startInformation.Environment["ASPNETCORE_AUTO_RELOAD_VDIR"] = "/";
        AppendEnvironmentListValue(
            startInformation,
            "DOTNET_STARTUP_HOOKS",
            browserRefreshMiddlewareAssemblyPath,
            Path.PathSeparator);
        AppendEnvironmentListValue(
            startInformation,
            "ASPNETCORE_HOSTINGSTARTUPASSEMBLIES",
            Path.GetFileNameWithoutExtension(browserRefreshMiddlewareAssemblyPath),
            ';');
        startInformation.Environment["DOTNET_MODIFIABLE_ASSEMBLIES"] = "debug";
        startInformation.Environment["__ASPNETCORE_BROWSER_TOOLS"] = "true";

        _process = new Process { StartInfo = startInformation };
        _process.OutputDataReceived += (_, arguments) =>
            CaptureRunOutput("stdout", arguments.Data);
        _process.ErrorDataReceived += (_, arguments) =>
            CaptureRunOutput("stderr", arguments.Data);
        if (!_process.Start())
        {
            throw new InvalidOperationException(
                "Could not start the Visual Studio-shaped Browser RunHost process.");
        }

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        await WaitForApplicationAsync();
        _workerProcessIdentifier = await WaitForWorkerAsync();
    }

    internal void RequireRunning()
    {
        if (_process is null || _process.HasExited)
        {
            throw new InvalidOperationException(
                "The Visual Studio-shaped Browser RunHost process exited before the scenario completed.\n"
                + string.Join('\n', _runOutput.TakeLast(40)));
        }
    }

    internal async Task StopAsync()
    {
        if (_stopped)
        {
            return;
        }

        _stopped = true;
        Process? process = _process;
        if (process is not null)
        {
            _workerProcessIdentifier ??= TryReadWorkerProcessIdentifier();
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            process.Dispose();
            _process = null;
        }

        await WriteOutputAsync();
        if (_workerProcessIdentifier is int workerProcessIdentifier)
        {
            await RequireProcessExitAsync(workerProcessIdentifier);
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private async Task RunBuildAsync()
    {
        ProcessStartInfo startInformation = CreateDotNetStartInformation();
        foreach (string argument in new[]
        {
            "build",
            ProjectPath,
            "--configuration",
            "Debug",
            "--runtime",
            "browser-wasm",
            "--no-restore",
            "-warnaserror",
        })
        {
            startInformation.ArgumentList.Add(argument);
        }

        using Process process = new() { StartInfo = startInformation };
        process.OutputDataReceived += (_, arguments) =>
            CaptureBuildOutput("stdout", arguments.Data);
        process.ErrorDataReceived += (_, arguments) =>
            CaptureBuildOutput("stderr", arguments.Data);
        if (!process.Start())
        {
            throw new InvalidOperationException(
                "Could not start the ordinary Debug build for the Visual Studio-shaped scenario.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"The ordinary Debug build failed with exit code {process.ExitCode}.\n"
                + string.Join('\n', _buildOutput.TakeLast(80)));
        }
    }

    private ProcessStartInfo CreateDotNetStartInformation()
    {
        ProcessStartInfo startInformation = new()
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            WorkingDirectory = ProjectDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInformation.Environment["DOTNET_NOLOGO"] = "1";
        startInformation.Environment["ViuConsumerVersion"] = ViuVersion;
        startInformation.Environment["AllowMissingPrunePackageData"] = "true";
        return startInformation;
    }

    private async Task<string> ResolveBrowserRefreshMiddlewareAssemblyPathAsync()
    {
        string dotNetHostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")
            ?? Environment.ProcessPath
            ?? throw new InvalidOperationException(
                "The .NET host path is unavailable for the BrowserRefresh harness.");
        if (!Path.IsPathFullyQualified(dotNetHostPath)
            || !File.Exists(dotNetHostPath))
        {
            throw new FileNotFoundException(
                "The BrowserRefresh harness requires an absolute .NET host path.",
                dotNetHostPath);
        }

        ProcessStartInfo versionStartInformation = CreateDotNetStartInformation();
        versionStartInformation.ArgumentList.Add("--version");
        using Process versionProcess = new() { StartInfo = versionStartInformation };
        if (!versionProcess.Start())
        {
            throw new InvalidOperationException(
                "Could not start the .NET SDK version probe for the BrowserRefresh harness.");
        }

        Task<string> standardOutput = versionProcess.StandardOutput.ReadToEndAsync();
        Task<string> standardError = versionProcess.StandardError.ReadToEndAsync();
        await versionProcess.WaitForExitAsync();
        string sdkVersion = (await standardOutput).Trim();
        string versionError = await standardError;
        if (versionProcess.ExitCode != 0 || string.IsNullOrWhiteSpace(sdkVersion))
        {
            throw new InvalidOperationException(
                "The .NET SDK version probe failed for the BrowserRefresh harness. "
                + versionError);
        }

        string dotNetRoot = Path.GetDirectoryName(dotNetHostPath)
            ?? throw new InvalidOperationException(
                "The .NET host path has no parent directory.");
        string assemblyPath = Path.Combine(
            dotNetRoot,
            "sdk",
            sdkVersion,
            "DotnetTools",
            "dotnet-watch",
            sdkVersion,
            "tools",
            "net10.0",
            "any",
            "hotreload",
            "net6.0",
            "Microsoft.AspNetCore.Watch.BrowserRefresh.dll");
        RequireFile(
            assemblyPath,
            $"BrowserRefresh middleware for .NET SDK {sdkVersion}");
        return assemblyPath;
    }

    private static void AppendEnvironmentListValue(
        ProcessStartInfo startInformation,
        string name,
        string value,
        char separator)
    {
        startInformation.Environment.TryGetValue(name, out string? existingValue);
        string[] existingValues = string.IsNullOrWhiteSpace(existingValue)
            ? Array.Empty<string>()
            : existingValue.Split(
                separator,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        StringComparer comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        if (existingValues.Contains(value, comparer))
        {
            return;
        }

        startInformation.Environment[name] = existingValues.Length == 0
            ? value
            : string.Join(separator, existingValues) + separator + value;
    }

    private async Task WaitForApplicationAsync()
    {
        using SocketsHttpHandler handler = new() { UseProxy = false };
        using HttpClient client = new(handler)
        {
            Timeout = TimeSpan.FromSeconds(2),
        };
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < StartupTimeout)
        {
            RequireRunning();
            Uri? address = _address;
            if (address is not null)
            {
                try
                {
                    using HttpResponseMessage response = await client.GetAsync(address);
                    if (response.IsSuccessStatusCode)
                    {
                        return;
                    }
                }
                catch (HttpRequestException)
                {
                }
                catch (TaskCanceledException)
                {
                }
            }

            await Task.Delay(100);
        }

        throw new TimeoutException(
            "Timed out waiting for the Visual Studio-shaped Browser application address.\n"
            + string.Join('\n', _runOutput.TakeLast(40)));
    }

    private async Task<int> WaitForWorkerAsync()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < StartupTimeout)
        {
            RequireRunning();
            int? processIdentifier = TryReadWorkerProcessIdentifier();
            if (processIdentifier is int value && IsProcessRunning(value))
            {
                return value;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException(
            $"Timed out waiting for the RunHost-owned generated-asset worker state at {WorkerStatePath}.\n"
            + string.Join('\n', _runOutput.TakeLast(40)));
    }

    private int? TryReadWorkerProcessIdentifier()
    {
        try
        {
            if (!File.Exists(WorkerStatePath))
            {
                return null;
            }

            const string prefix = "worker=";
            string? line = File.ReadLines(WorkerStatePath)
                .FirstOrDefault(candidate => candidate.StartsWith(
                    prefix,
                    StringComparison.Ordinal));
            return line is not null
                && int.TryParse(
                    line.AsSpan(prefix.Length),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int processIdentifier)
                ? processIdentifier
                : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private void CaptureBuildOutput(string stream, string? value)
    {
        if (value is not null)
        {
            _buildOutput.Enqueue($"[{stream}] {value}");
        }
    }

    private void CaptureRunOutput(string stream, string? value)
    {
        if (value is null)
        {
            return;
        }

        _runOutput.Enqueue($"[{stream}] {value}");
        const string addressPrefix = "App url: http://";
        if (_address is null
            && value.StartsWith(addressPrefix, StringComparison.Ordinal)
            && Uri.TryCreate(
                value[addressPrefix.Length..].Insert(0, "http://"),
                UriKind.Absolute,
                out Uri? address))
        {
            _address = address;
        }
    }

    private async Task WriteOutputAsync()
    {
        await File.WriteAllLinesAsync(
            Path.Combine(_artifactDirectory, "ordinary-debug-build.log"),
            _buildOutput);
        await File.WriteAllLinesAsync(
            Path.Combine(_artifactDirectory, "run-host.log"),
            _runOutput);
    }

    private static async Task RequireProcessExitAsync(int processIdentifier)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(10))
        {
            if (!IsProcessRunning(processIdentifier))
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new InvalidOperationException(
            $"RunHost-owned generated-asset worker process {processIdentifier} survived session shutdown.");
    }

    private static bool IsProcessRunning(int processIdentifier)
    {
        try
        {
            using Process process = Process.GetProcessById(processIdentifier);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void RequireFile(string path, string description)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"The {description} does not exist.",
                path);
        }
    }

    private static void RequireMissingFile(string path, string description)
    {
        if (File.Exists(path))
        {
            throw new InvalidOperationException(
                $"The {description} must not exist before this phase: {path}");
        }
    }
}
