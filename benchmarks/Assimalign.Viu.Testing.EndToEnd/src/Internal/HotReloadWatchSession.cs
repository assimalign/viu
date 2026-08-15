using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Assimalign.Viu.Testing.EndToEnd;

internal sealed class HotReloadWatchSession : IAsyncDisposable
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(3);
    private readonly string _artifactDirectory;
    private readonly ConcurrentQueue<string> _output = [];
    private Uri? _address;
    private Process? _process;
    private int? _workerProcessIdentifier;
    private bool _stopped;

    internal HotReloadWatchSession(
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
                "The hot-reload project path has no parent directory.",
                nameof(projectPath));
        _artifactDirectory = Path.GetFullPath(artifactDirectory);
        ViuVersion = viuVersion;
        MainSourcePath = Path.Combine(ProjectDirectory, "HotReloadPage.vue");
        ComponentBundlePath = Path.Combine(
            ProjectDirectory,
            "obj",
            "Debug",
            "net10.0",
            "viu",
            "EndToEndHotReloadApp.viu.css");
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
    }

    internal Uri Address => _address
        ?? throw new InvalidOperationException(
            "The watch application has not reported its address.");

    internal string ProjectPath { get; }

    internal string ProjectDirectory { get; }

    internal string ViuVersion { get; }

    internal string MainSourcePath { get; }

    internal string ComponentBundlePath { get; }

    internal string CssEventLogPath { get; }

    internal string WorkerStatePath { get; }

    internal async Task StartAsync()
    {
        RequireFile(ProjectPath, "hot-reload fixture project");
        RequireFile(MainSourcePath, "mounted .vue source");
        Directory.CreateDirectory(_artifactDirectory);

        ProcessStartInfo startInfo = new()
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            WorkingDirectory = ProjectDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (string argument in new[]
        {
            "watch",
            "--verbose",
            "--project",
            ProjectPath,
            "--configuration",
            "Debug",
            "--runtime",
            "browser-wasm",
            "--non-interactive",
            "--launch-profile",
            "EndToEndHotReloadApp",
            "--no-restore",
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        // [V01.01.12.31], #349: Keep dotnet-watch's browser observer enabled while Playwright owns
        // the only interactive browser. The harness executable absorbs the automatic launch.
        string browserLaunchSinkPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assimalign.Viu.Testing.EndToEnd"
                + (OperatingSystem.IsWindows() ? ".exe" : string.Empty));
        RequireFile(browserLaunchSinkPath, "dotnet-watch browser launch sink");
        startInfo.Environment["DOTNET_WATCH_BROWSER_PATH"] = browserLaunchSinkPath;
        startInfo.Environment["DOTNET_WATCH_SUPPRESS_EMOJIS"] = "1";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.Environment["VIU_END_TO_END_BROWSER_LAUNCH_SINK"] = "1";
        startInfo.Environment["ViuConsumerVersion"] = ViuVersion;

        _process = new Process { StartInfo = startInfo };
        _process.OutputDataReceived += (_, arguments) =>
            CaptureOutput("stdout", arguments.Data);
        _process.ErrorDataReceived += (_, arguments) =>
            CaptureOutput("stderr", arguments.Data);
        if (!_process.Start())
        {
            throw new InvalidOperationException("Could not start the owned dotnet watch process.");
        }

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        await WaitForApplicationAsync();
        _workerProcessIdentifier = await WaitForWorkerAsync();
        RequireFile(ComponentBundlePath, "initial component stylesheet bundle");
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

    internal void RequireRunning()
    {
        if (_process is null || _process.HasExited)
        {
            throw new InvalidOperationException(
                "The owned dotnet watch process exited before the scenario completed.\n"
                + string.Join('\n', _output.TakeLast(40)));
        }
    }

    internal int CountOutputLinesContaining(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        return _output.Count(line => line.Contains(text, StringComparison.Ordinal));
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
            "Timed out waiting for the packaged watch application address.\n"
            + string.Join('\n', _output.TakeLast(40)));
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
            $"Timed out waiting for the CSS worker state at {WorkerStatePath}.");
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

    private async Task WriteOutputAsync()
    {
        string outputPath = Path.Combine(
            _artifactDirectory,
            "dotnet-watch.log");
        await File.WriteAllLinesAsync(outputPath, _output);
    }

    private void CaptureOutput(string stream, string? value)
    {
        if (value is not null)
        {
            _output.Enqueue($"[{stream}] {value}");
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
            $"Owned CSS worker process {processIdentifier} survived dotnet watch tree shutdown.");
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
}
