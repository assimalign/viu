using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Sdk.Browser.RunHost.Tests;

public sealed class GeneratedAssetWorkerHostTests
{
    // [V01.01.12.05.03], #370: RunHost must preserve the active watch worker's topology
    // using the same kernel identity as the watch-list task, not a Linux wall-clock estimate.
    [Theory]
    [InlineData("dotnet")]
    [InlineData("worker with spaces")]
    [InlineData("worker ) with (parentheses)")]
    public void TryParseLinuxStartClockTicks_ProcessNameContainsDelimiters_ReadsFieldTwentyTwo(
        string processName)
    {
        string status = CreateLinuxProcessStatus(processName, "987654321");

        GeneratedAssetWorkerHost.TryParseLinuxStartClockTicks(status, out long startClockTicks)
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
        GeneratedAssetWorkerHost.TryParseLinuxStartClockTicks(status, out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("9223372036854775808")]
    public void TryParseLinuxStartClockTicks_InvalidStartIdentity_RejectsStatus(string startIdentity)
    {
        string status = CreateLinuxProcessStatus("dotnet", startIdentity);

        GeneratedAssetWorkerHost.TryParseLinuxStartClockTicks(status, out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void IsWorkerActive_CurrentProcess_RequiresMatchingPlatformIdentity(
        long startIdentityOffset,
        bool expectedActive)
    {
        using Process process = Process.GetCurrentProcess();
        string state = "worker=" + process.Id.ToString(CultureInfo.InvariantCulture) + "\n";
        if (OperatingSystem.IsLinux())
        {
            string status = File.ReadAllText("/proc/self/stat");
            string[] fields = status.Substring(status.LastIndexOf(')') + 2)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            long startClockTicks = long.Parse(fields[19], CultureInfo.InvariantCulture);
            // Deliberately incorrect wall-clock ticks must not reject a matching Linux token.
            state += "worker-start=1\nworker-start-clock-ticks="
                + (startClockTicks + startIdentityOffset).ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            state += "worker-start="
                + (process.StartTime.ToUniversalTime().Ticks + startIdentityOffset)
                    .ToString(CultureInfo.InvariantCulture);
        }

        WithStateFile(state, stateFilePath =>
            GeneratedAssetWorkerHost.IsWorkerActive(stateFilePath).ShouldBe(expectedActive));
    }

    [Fact]
    public void IsWorkerActive_CurrentProcessWithoutPlatformIdentity_RejectsState()
    {
        using Process process = Process.GetCurrentProcess();
        string state = "worker=" + process.Id.ToString(CultureInfo.InvariantCulture) + "\n";
        if (OperatingSystem.IsLinux())
        {
            // A legacy Linux wall-clock record must not stand in for the missing kernel token.
            state += "worker-start="
                + process.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture);
        }

        WithStateFile(state, stateFilePath =>
            GeneratedAssetWorkerHost.IsWorkerActive(stateFilePath).ShouldBeFalse());
    }

    [Fact]
    public void ReadManagedStylesheetPaths_ValidConfiguration_ReturnsDistinctNormalizedCssRoutes()
    {
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            "viu-run-host-configuration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        string configurationFilePath = Path.Combine(
            directoryPath,
            "worker.configuration");
        try
        {
            File.WriteAllLines(
                configurationFilePath,
                [
                    "viu-generated-asset-worker-configuration-v1",
                    "asset-begin",
                    Encode("static-web-asset-path", "wwwroot/component.css"),
                    "asset-end",
                    "asset-begin",
                    Encode("static-web-asset-path", "wwwroot/scripts/generated.js"),
                    "asset-end",
                    "asset-begin",
                    Encode("static-web-asset-path", "wwwroot/utilities.css"),
                    "asset-end",
                    "asset-begin",
                    Encode("static-web-asset-path", "wwwroot/component.css"),
                    "asset-end",
                ],
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            IReadOnlyList<string> paths =
                GeneratedAssetWorkerHost.ReadManagedStylesheetPaths(
                    configurationFilePath);

            paths.ShouldBe(["/component.css", "/utilities.css"]);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void ReadManagedStylesheetPaths_UnsupportedHeader_ThrowsInvalidDataException()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(
                path,
                "unsupported",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            Action read = () =>
                GeneratedAssetWorkerHost.ReadManagedStylesheetPaths(path);

            read.ShouldThrow<InvalidDataException>();
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string Encode(string name, string value) =>
        name + ":" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static string CreateLinuxProcessStatus(string processName, string startClockTicks) =>
        "123 (" + processName + ") R 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 20 21 "
        + startClockTicks + " 23 24";

    private static void WithStateFile(string state, Action<string> assertion)
    {
        DirectoryInfo? repositoryDirectory = new(AppContext.BaseDirectory);
        while (repositoryDirectory is not null
            && !File.Exists(Path.Combine(repositoryDirectory.FullName, "Assimalign.Viu.slnx")))
        {
            repositoryDirectory = repositoryDirectory.Parent;
        }

        repositoryDirectory.ShouldNotBeNull("The test output must remain beneath the Viu repository.");
        string directory = Path.Combine(repositoryDirectory.FullName, "_out", "issue370-runhost-tests");
        Directory.CreateDirectory(directory);
        string stateFilePath = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".state");
        try
        {
            File.WriteAllText(stateFilePath, state);
            assertion(stateFilePath);
        }
        finally
        {
            File.Delete(stateFilePath);
        }
    }
}
