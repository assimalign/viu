using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.PublishBudget.Tests;

public sealed class StartupBudgetTests : IDisposable
{
    private const string SampleName = "EndToEndBrowserApp";
    private readonly List<string> temporaryDirectories = new();

    [Fact]
    public void Gate_MedianWithinBudgetAndTolerance_ReportsPassAndExitsZero()
    {
        var manifest = CreateManifest(budgetMilliseconds: 100, toleranceMilliseconds: 10);
        var results = CreateResults(CreateStartupValues(105));

        var run = RunGate(manifest, results);

        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("Actual median:      105.0 ms");
        run.Output.ShouldContain("Result:             PASS");
    }

    [Fact]
    public void Gate_MedianOverEffectiveCeiling_NamesBudgetAndDeltaAndExitsOne()
    {
        var manifest = CreateManifest(budgetMilliseconds: 100, toleranceMilliseconds: 10);
        var results = CreateResults(CreateStartupValues(135));

        var run = RunGate(manifest, results);

        run.ExitCode.ShouldBe(1);
        run.Output.ShouldContain($"{SampleName} startupBudgetMilliseconds exceeded by 25.0 ms");
        run.Output.ShouldContain("actual 135.0 ms");
        run.Output.ShouldContain("budget 100.0 ms");
        run.Output.ShouldContain("delta vs base n/a");
    }

    [Fact]
    public void Gate_FewerThanTenMeasuredRuns_FailsActionablyAsConfigurationError()
    {
        var manifest = CreateManifest(budgetMilliseconds: 100, toleranceMilliseconds: 10);
        var results = CreateResults(CreateStartupValues(100, count: 9));

        var run = RunGate(manifest, results);

        run.ExitCode.ShouldBe(2);
        run.Output.ShouldContain("at least 10 post-warm-up runs are required");
    }

    [Fact]
    public void Gate_WithBaselineResults_ReportsSignedBaseDelta()
    {
        var manifest = CreateManifest(budgetMilliseconds: 200, toleranceMilliseconds: 0);
        var baseline = CreateResults(CreateStartupValues(90));
        var results = CreateResults(CreateStartupValues(100));

        var run = RunGate(manifest, results, "-BaselineResultsPath", baseline);

        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("Delta vs base:      +10.0 ms");
    }

    [Fact]
    public void Gate_WithBaselineManifest_ReportsBaseRevisionDeltaWithoutRunningBrowserTwice()
    {
        var manifest = CreateManifest(budgetMilliseconds: 200, toleranceMilliseconds: 0);
        var baselineManifest = CreateManifest(
            budgetMilliseconds: 200,
            toleranceMilliseconds: 0,
            baselineMilliseconds: 90);
        var results = CreateResults(CreateStartupValues(100));

        var run = RunGate(manifest, results, "-BaselineManifestPath", baselineManifest);

        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("Delta vs base:      +10.0 ms");
    }

    [Fact]
    public void Gate_ReportedMedianDoesNotMatchSamples_FailsAsConfigurationError()
    {
        var manifest = CreateManifest(budgetMilliseconds: 200, toleranceMilliseconds: 0);
        var results = CreateResults(CreateStartupValues(100), reportedMedian: 101);

        var run = RunGate(manifest, results);

        run.ExitCode.ShouldBe(2);
        run.Output.ShouldContain("does not match the calculated median");
    }

    [Fact]
    public void Gate_NonChromiumMeasurement_FailsAsConfigurationError()
    {
        var manifest = CreateManifest(budgetMilliseconds: 200, toleranceMilliseconds: 0);
        var results = CreateResults(CreateStartupValues(100), browserEngine: "Firefox");

        var run = RunGate(manifest, results);

        run.ExitCode.ShouldBe(2);
        run.Output.ShouldContain("browserEngine must be 'Chromium'; found 'Firefox'");
    }

    [Theory]
    [InlineData("first-render")]
    [InlineData(null)]
    public void Gate_WrongOrMissingMeasurementName_FailsAsConfigurationError(string? measurement)
    {
        var manifest = CreateManifest(budgetMilliseconds: 200, toleranceMilliseconds: 0);
        var results = CreateResults(CreateStartupValues(100), measurement: measurement);

        var run = RunGate(manifest, results);

        run.ExitCode.ShouldBe(2);
        run.Output.ShouldContain("measurement");
        if (measurement is not null)
        {
            run.Output.ShouldContain("boot-to-interactive");
        }
    }

    [Fact]
    public void Gate_BaselineWithDifferentMeasurementName_FailsAsConfigurationError()
    {
        var manifest = CreateManifest(budgetMilliseconds: 200, toleranceMilliseconds: 0);
        var baseline = CreateResults(CreateStartupValues(90), measurement: "first-render");
        var results = CreateResults(CreateStartupValues(100));

        var run = RunGate(manifest, results, "-BaselineResultsPath", baseline);

        run.ExitCode.ShouldBe(2);
        run.Output.ShouldContain("Baseline measurement 'first-render' does not match 'boot-to-interactive'");
    }

    private static double[] CreateStartupValues(double value, int count = 10)
    {
        var values = new double[count];
        Array.Fill(values, value);
        return values;
    }

    private string CreateManifest(
        double budgetMilliseconds,
        double toleranceMilliseconds,
        double? baselineMilliseconds = null)
    {
        var path = Path.Combine(CreateTemporaryDirectory(), "PublishBudgets.json");
        var manifest = new
        {
            samples = new[]
            {
                new
                {
                    name = SampleName,
                    startupBudgetMilliseconds = budgetMilliseconds,
                    startupToleranceMilliseconds = toleranceMilliseconds,
                    startupBaselineMilliseconds = baselineMilliseconds,
                },
            },
        };
        File.WriteAllText(path, JsonSerializer.Serialize(manifest), Encoding.UTF8);
        return path;
    }

    private string CreateResults(
        double[] values,
        double? reportedMedian = null,
        string browserEngine = "Chromium",
        string? measurement = "boot-to-interactive")
    {
        var path = Path.Combine(CreateTemporaryDirectory(), "startup-results.json");
        var results = new
        {
            schemaVersion = 1,
            generatedUtc = DateTimeOffset.UtcNow,
            sample = SampleName,
            measurement,
            browserEngine,
            warmupRuns = 1,
            measuredRuns = values.Length,
            startupMilliseconds = values,
            medianStartupMilliseconds = reportedMedian ?? values[values.Length / 2],
        };
        File.WriteAllText(path, JsonSerializer.Serialize(results), Encoding.UTF8);
        return path;
    }

    private GateRun RunGate(string manifestPath, string resultsPath, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(LocateGateScript());
        startInfo.ArgumentList.Add("-ManifestPath");
        startInfo.ArgumentList.Add(manifestPath);
        startInfo.ArgumentList.Add("-ResultsPath");
        startInfo.ArgumentList.Add(resultsPath);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start pwsh for the startup budget gate.");
        var output = new StringBuilder();
        output.Append(process.StandardOutput.ReadToEnd());
        output.Append(process.StandardError.ReadToEnd());
        process.WaitForExit();
        return new GateRun(process.ExitCode, output.ToString());
    }

    private static string LocateGateScript()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var direct = Path.Combine(directory.FullName, "Test-StartupBudget.ps1");
            if (File.Exists(direct))
            {
                return direct;
            }
            var nested = Path.Combine(directory.FullName, "scripts", "Test-StartupBudget.ps1");
            if (File.Exists(nested))
            {
                return nested;
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Could not locate scripts/Test-StartupBudget.ps1 above the test assembly.");
    }

    private string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "viu-startup-budget-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(path);
        temporaryDirectories.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var directory in temporaryDirectories)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private sealed record GateRun(int ExitCode, string Output);
}
