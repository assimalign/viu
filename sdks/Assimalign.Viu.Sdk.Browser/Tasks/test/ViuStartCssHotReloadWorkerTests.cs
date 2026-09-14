using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Sdk.Browser.Tasks;

namespace Assimalign.Viu.Sdk.Browser.Tasks.Tests;

// [V01.01.12.05.03], #370 pins process identity and diagnosable worker readiness failures.
public sealed class ViuStartCssHotReloadWorkerTests : IDisposable
{
    private readonly string directory = CreateTemporaryDirectory();

    [Theory]
    [InlineData("dotnet")]
    [InlineData("worker with spaces")]
    [InlineData("worker ) with (parentheses)")]
    public void TryParseLinuxStartClockTicks_ProcessNameContainsDelimiters_ReadsFieldTwentyTwo(
        string processName)
    {
        var status = CreateLinuxProcessStatus(processName, "987654321");

        CssHotReloadWorkerState.TryParseLinuxStartClockTicks(status, out var startClockTicks)
            .ShouldBeTrue();

        startClockTicks.ShouldBe(987654321L);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123 dotnet R 4 5 6")]
    [InlineData("123 (dotnet) R 4 5 6")]
    [InlineData("123 (dotnet")]
    public void TryParseLinuxStartClockTicks_MissingRequiredFields_RejectsStatus(string status)
    {
        CssHotReloadWorkerState.TryParseLinuxStartClockTicks(status, out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("9223372036854775808")]
    public void TryParseLinuxStartClockTicks_InvalidStartIdentity_RejectsStatus(string startIdentity)
    {
        var status = CreateLinuxProcessStatus("dotnet", startIdentity);

        CssHotReloadWorkerState.TryParseLinuxStartClockTicks(status, out _).ShouldBeFalse();
    }

    [Fact]
    public void TryReadLiveWorker_CurrentProcessWithMatchingPlatformIdentity_AcceptsState()
    {
        using var process = Process.GetCurrentProcess();
        var stateFilePath = WriteCurrentProcessState(process);

        CssHotReloadWorkerState.TryReadLiveWorker(stateFilePath, out var processIdentifier)
            .ShouldBeTrue();

        processIdentifier.ShouldBe(process.Id);
    }

    [Fact]
    public void TryReadLiveWorker_ReusedProcessIdentifierWithDifferentStartIdentity_RejectsState()
    {
        using var process = Process.GetCurrentProcess();
        var stateFilePath = WriteCurrentProcessState(process, startIdentityOffset: 1);

        CssHotReloadWorkerState.TryReadLiveWorker(stateFilePath, out _).ShouldBeFalse();
    }

    [Fact]
    public void TryReadLiveWorker_MissingStateFile_RejectsState()
    {
        CssHotReloadWorkerState.TryReadLiveWorker(
            Path.Combine(directory, "missing.state"),
            out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("worker=0\nworker-start=1\nworker-start-clock-ticks=1")]
    [InlineData("worker=-1\nworker-start=1\nworker-start-clock-ticks=1")]
    [InlineData("worker=invalid\nworker-start=1\nworker-start-clock-ticks=1")]
    [InlineData("worker=2147483647\nworker-start=1\nworker-start-clock-ticks=1")]
    public void TryReadLiveWorker_InvalidOrAbsentProcess_RejectsState(string state)
    {
        var stateFilePath = WriteState(state);

        CssHotReloadWorkerState.TryReadLiveWorker(stateFilePath, out _).ShouldBeFalse();
    }

    [Fact]
    public void TryReadLiveWorker_ExitedProcess_RejectsState()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
            Arguments = OperatingSystem.IsWindows() ? "/d /c exit 0" : "-c \"exit 0\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var process = Process.Start(startInfo);
        process.ShouldNotBeNull();
        process.WaitForExit(5000).ShouldBeTrue();
        var stateFilePath = WriteState("worker=" +
            process.Id.ToString(CultureInfo.InvariantCulture) +
            "\nworker-start=1\nworker-start-clock-ticks=1");

        CssHotReloadWorkerState.TryReadLiveWorker(stateFilePath, out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("worker-start=0\nworker-start-clock-ticks=0")]
    [InlineData("worker-start=invalid\nworker-start-clock-ticks=invalid")]
    public void TryReadLiveWorker_CurrentProcessWithoutValidStartIdentity_RejectsState(string identity)
    {
        using var process = Process.GetCurrentProcess();
        var stateFilePath = WriteState("worker=" +
            process.Id.ToString(CultureInfo.InvariantCulture) + "\n" + identity);

        CssHotReloadWorkerState.TryReadLiveWorker(stateFilePath, out _).ShouldBeFalse();
    }

    [Fact]
    public void FormatFailure_CapturedOutput_IncludesPolledPathAndBothStreams()
    {
        var diagnostics = new CssHotReloadWorkerDiagnostics();
        var stateFilePath = Path.Combine(directory, "worker.state");
        diagnostics.AppendStandardOutput("worker initialized a watcher");
        diagnostics.AppendStandardError("worker could not open the output directory");
        diagnostics.AppendStandardOutput(null);
        diagnostics.AppendStandardError(null);

        var failure = diagnostics.FormatFailure(stateFilePath);

        failure.ShouldContain(stateFilePath);
        failure.ShouldContain("worker initialized a watcher");
        failure.ShouldContain("worker could not open the output directory");
    }

    [Fact]
    public void FormatFailure_LargeCapturedOutput_PreservesBoundedTailOfEachStream()
    {
        var diagnostics = new CssHotReloadWorkerDiagnostics();
        var stateFilePath = Path.Combine(directory, "worker.state");
        diagnostics.AppendStandardOutput("discarded stdout prefix" + new string('O', 20000));
        diagnostics.AppendStandardError("discarded stderr prefix" + new string('E', 20000));
        diagnostics.AppendStandardOutput("final stdout evidence");
        diagnostics.AppendStandardError("final stderr evidence");

        var failure = diagnostics.FormatFailure(stateFilePath);

        failure.ShouldContain("final stdout evidence");
        failure.ShouldContain("final stderr evidence");
        failure.ShouldNotContain("discarded stdout prefix");
        failure.ShouldNotContain("discarded stderr prefix");
        failure.Count(character => character == 'O').ShouldBeLessThanOrEqualTo(8192);
        failure.Count(character => character == 'E').ShouldBeLessThanOrEqualTo(8192);
        failure.Length.ShouldBeLessThan(18000);
    }

    [Fact]
    public void FormatFailure_DurableWorkerLogs_IncludesPolledPathAndLatestFileOutput()
    {
        var diagnostics = new CssHotReloadWorkerDiagnostics();
        var stateFilePath = Path.Combine(directory, "worker.state");
        File.WriteAllText(stateFilePath + ".stdout.log", "durable worker stdout evidence");
        File.WriteAllText(stateFilePath + ".stderr.log", "durable worker stderr evidence");

        var failure = diagnostics.FormatFailure(stateFilePath);

        failure.ShouldContain(stateFilePath);
        failure.ShouldContain("durable worker stdout evidence");
        failure.ShouldContain("durable worker stderr evidence");
    }

    [Fact]
    public void FormatFailure_LargeDurableWorkerLogs_PreservesBoundedTailOfEachStream()
    {
        var diagnostics = new CssHotReloadWorkerDiagnostics();
        var stateFilePath = Path.Combine(directory, "worker.state");
        File.WriteAllText(stateFilePath + ".stdout.log",
            "discarded stdout prefix" + new string('O', 20000) + "final stdout evidence");
        File.WriteAllText(stateFilePath + ".stderr.log",
            "discarded stderr prefix" + new string('E', 20000) + "final stderr evidence");

        var failure = diagnostics.FormatFailure(stateFilePath);

        failure.ShouldContain("final stdout evidence");
        failure.ShouldContain("final stderr evidence");
        failure.ShouldNotContain("discarded stdout prefix");
        failure.ShouldNotContain("discarded stderr prefix");
        failure.Count(character => character == 'O').ShouldBeLessThanOrEqualTo(8192);
        failure.Count(character => character == 'E').ShouldBeLessThanOrEqualTo(8192);
        failure.Length.ShouldBeLessThan(18000);
    }

    [Fact]
    public void FormatFailure_ConcurrentStreamCallbacks_PreservesEveryShortMessage()
    {
        var diagnostics = new CssHotReloadWorkerDiagnostics();
        var stateFilePath = Path.Combine(directory, "worker.state");
        Parallel.For(0, 40, index =>
        {
            diagnostics.AppendStandardOutput("stdout-message-" +
                index.ToString("D2", CultureInfo.InvariantCulture));
            diagnostics.AppendStandardError("stderr-message-" +
                index.ToString("D2", CultureInfo.InvariantCulture));
            diagnostics.FormatFailure(stateFilePath);
        });

        var failure = diagnostics.FormatFailure(stateFilePath);

        for (var index = 0; index < 40; index++)
        {
            failure.ShouldContain("stdout-message-" + index.ToString("D2", CultureInfo.InvariantCulture));
            failure.ShouldContain("stderr-message-" + index.ToString("D2", CultureInfo.InvariantCulture));
        }
    }

    public void Dispose()
    {
        var scratchRoot = Path.GetFullPath(Path.Combine(FindRepositoryDirectory(), "_out", "issue370-tests")) +
            Path.DirectorySeparatorChar;
        var resolvedDirectory = Path.GetFullPath(directory);
        resolvedDirectory.StartsWith(scratchRoot, StringComparison.Ordinal).ShouldBeTrue();
        Directory.Delete(resolvedDirectory, recursive: true);
    }

    private string WriteCurrentProcessState(Process process, long startIdentityOffset = 0)
    {
        var state = "worker=" + process.Id.ToString(CultureInfo.InvariantCulture) + "\n";
        if (OperatingSystem.IsLinux())
        {
            var status = File.ReadAllText("/proc/self/stat");
            var fields = status.Substring(status.LastIndexOf(')') + 2)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var startClockTicks = long.Parse(fields[19], CultureInfo.InvariantCulture);
            // UTC StartTime is estimated independently in each Linux process. The kernel token
            // must identify this live process even when the recorded UTC value is deliberately wrong.
            state += "worker-start=1\nworker-start-clock-ticks=" +
                (startClockTicks + startIdentityOffset).ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            state += "worker-start=" +
                (process.StartTime.ToUniversalTime().Ticks + startIdentityOffset)
                .ToString(CultureInfo.InvariantCulture);
        }

        return WriteState(state);
    }

    private string WriteState(string state)
    {
        var stateFilePath = Path.Combine(directory, "worker.state");
        File.WriteAllText(stateFilePath, state);
        return stateFilePath;
    }

    private static string CreateLinuxProcessStatus(string processName, string startClockTicks) =>
        "123 (" + processName + ") R 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 20 21 " +
        startClockTicks + " 23 24";

    private static string CreateTemporaryDirectory()
    {
        var temporaryDirectory = Path.Combine(
            FindRepositoryDirectory(),
            "_out",
            "issue370-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        return temporaryDirectory;
    }

    private static string FindRepositoryDirectory()
    {
        for (var candidate = new DirectoryInfo(AppContext.BaseDirectory);
            candidate is not null;
            candidate = candidate.Parent)
        {
            if (File.Exists(Path.Combine(candidate.FullName, "Assimalign.Viu.slnx")))
            {
                return candidate.FullName;
            }
        }

        throw new InvalidOperationException("The test output must remain beneath the Viu repository.");
    }
}
