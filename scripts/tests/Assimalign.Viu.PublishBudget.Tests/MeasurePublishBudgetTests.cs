using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.PublishBudget.Tests;

// Behavioral coverage for the publish-size budget gate
// (scripts/Measure-PublishBudget.ps1, [V01.01.12.06], #95). Each test builds a
// deterministic on-disk payload fixture, invokes the gate through pwsh in
// measure-only or gating mode, and asserts the exit code and machine-readable
// results. The real `dotnet publish` path is intentionally out of scope here (it
// is exercised by the budget-gates CI workflow); these tests pin the measurement,
// exclusion, gating, and delta logic that the gate decision rests on.
public sealed class MeasurePublishBudgetTests : IDisposable
{
    private const string SampleName = "Sample";

    private readonly List<string> temporaryDirectories = new();

    [Fact]
    public void Gate_PayloadWithinBudget_ReportsPassAndExitsZero()
    {
        var payload = CreatePayload(("app.wasm", CompressibleBytes(4096)));
        var manifest = CreateManifest(budgetBytes: 100_000_000);

        var run = RunGate(manifest, "-PublishDirectory", payload);

        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("PASS");
        run.Output.ShouldContain("within budget");
    }

    [Fact]
    public void Gate_PayloadOverBudget_ReportsOverBudgetAndExitsOne()
    {
        var payload = CreatePayload(("app.wasm", CompressibleBytes(4096)));
        var manifest = CreateManifest(budgetBytes: 1);

        var run = RunGate(manifest, "-PublishDirectory", payload);

        run.ExitCode.ShouldBe(1);
        run.Output.ShouldContain("OVER BUDGET");
    }

    [Fact]
    public void Gate_OverBudgetWithNoGate_ExitsZeroForInformationalMeasurement()
    {
        var payload = CreatePayload(("app.wasm", CompressibleBytes(4096)));
        var manifest = CreateManifest(budgetBytes: 1);

        var run = RunGate(manifest, "-PublishDirectory", payload, "-NoGate");

        run.ExitCode.ShouldBe(0);
    }

    [Fact]
    public void Gate_BrotliMeasurement_IsSmallerThanRawAndNonZero()
    {
        var payload = CreatePayload(("app.wasm", CompressibleBytes(65_536)));
        var manifest = CreateManifest(budgetBytes: 100_000_000);
        var resultsPath = ReserveResultsPath();

        var run = RunGate(manifest, "-PublishDirectory", payload, "-NoGate", "-ResultsPath", resultsPath);

        run.ExitCode.ShouldBe(0);
        var measured = ReadFirstSample(resultsPath);
        measured.CompressedPublishSizeBytes.ShouldBeGreaterThan(0);
        measured.CompressedPublishSizeBytes.ShouldBeLessThan(measured.RawPublishSizeBytes);
    }

    [Fact]
    public void Gate_PrecompressedAndSourceMapFiles_AreExcludedFromThePayload()
    {
        // The SDK emits *.br/*.gz duplicates next to each asset; compressing those
        // again (or counting *.map) would double count. Only the raw asset counts.
        var payload = CreatePayload(
            ("app.wasm", CompressibleBytes(2_000)),
            ("app.wasm.br", CompressibleBytes(500_000)),
            ("app.wasm.gz", CompressibleBytes(500_000)),
            ("bundle.js.map", CompressibleBytes(500_000)));
        var manifest = CreateManifest(budgetBytes: 100_000_000);
        var resultsPath = ReserveResultsPath();

        var run = RunGate(manifest, "-PublishDirectory", payload, "-NoGate", "-ResultsPath", resultsPath);

        run.ExitCode.ShouldBe(0);
        var measured = ReadFirstSample(resultsPath);
        measured.FileCount.ShouldBe(1);
        measured.RawPublishSizeBytes.ShouldBe(2_000);
    }

    [Fact]
    public void Gate_WithBaselineResults_RecordsBaselineForDeltaReporting()
    {
        var payload = CreatePayload(("app.wasm", CompressibleBytes(4_096)));
        var manifest = CreateManifest(budgetBytes: 100_000_000);
        const long baselineBytes = 123_456;
        var baselinePath = CreateBaselineResults(baselineBytes);
        var resultsPath = ReserveResultsPath();

        var run = RunGate(
            manifest,
            "-PublishDirectory",
            payload,
            "-NoGate",
            "-BaselineResultsPath",
            baselinePath,
            "-ResultsPath",
            resultsPath);

        run.ExitCode.ShouldBe(0);
        run.Output.ShouldNotContain("n/a");
        var measured = ReadFirstSample(resultsPath);
        measured.BaselineBytes.ShouldBe(baselineBytes);
    }

    [Fact]
    public void Gate_MissingManifest_ExitsTwoForConfigurationError()
    {
        var payload = CreatePayload(("app.wasm", CompressibleBytes(1_024)));
        var missingManifest = Path.Combine(CreateTemporaryDirectory(), "does-not-exist.json");

        var run = RunGate(missingManifest, "-PublishDirectory", payload);

        run.ExitCode.ShouldBe(2);
    }

    // --- fixtures ---------------------------------------------------------------

    private static byte[] CompressibleBytes(int count)
    {
        // Highly compressible so the brotli size is always well under the raw size.
        var bytes = new byte[count];
        Array.Fill(bytes, (byte)'A');
        return bytes;
    }

    private string CreatePayload(params (string Name, byte[] Content)[] files)
    {
        var root = CreateTemporaryDirectory();
        var wwwroot = Path.Combine(root, "wwwroot");
        Directory.CreateDirectory(wwwroot);
        foreach (var file in files)
        {
            File.WriteAllBytes(Path.Combine(wwwroot, file.Name), file.Content);
        }
        return root;
    }

    private string CreateManifest(long budgetBytes)
    {
        var manifestPath = Path.Combine(CreateTemporaryDirectory(), "PublishBudgets.json");
        var manifest = new
        {
            samples = new[]
            {
                new
                {
                    name = SampleName,
                    project = "unused-in-publish-directory-mode.csproj",
                    compressedPublishSizeBytes = budgetBytes,
                    startupBudgetMilliseconds = 1_000,
                },
            },
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest), Encoding.UTF8);
        return manifestPath;
    }

    private string CreateBaselineResults(long compressedPublishSizeBytes)
    {
        var baselinePath = Path.Combine(CreateTemporaryDirectory(), "baseline-results.json");
        var baseline = new
        {
            configuration = "Release",
            samples = new[]
            {
                new
                {
                    name = SampleName,
                    compressedPublishSizeBytes,
                },
            },
        };
        File.WriteAllText(baselinePath, JsonSerializer.Serialize(baseline), Encoding.UTF8);
        return baselinePath;
    }

    private string ReserveResultsPath()
        => Path.Combine(CreateTemporaryDirectory(), "results.json");

    private static SampleMeasurement ReadFirstSample(string resultsPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(resultsPath));
        var sample = document.RootElement.GetProperty("samples")[0];
        long? baseline = sample.TryGetProperty("baselineBytes", out var baselineElement)
            && baselineElement.ValueKind is not JsonValueKind.Null
                ? baselineElement.GetInt64()
                : null;
        return new SampleMeasurement(
            sample.GetProperty("compressedPublishSizeBytes").GetInt64(),
            sample.GetProperty("rawPublishSizeBytes").GetInt64(),
            sample.GetProperty("fileCount").GetInt32(),
            baseline);
    }

    private GateRun RunGate(string manifestPath, params string[] arguments)
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
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start pwsh for the budget gate.");
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
            var direct = Path.Combine(directory.FullName, "Measure-PublishBudget.ps1");
            if (File.Exists(direct))
            {
                return direct;
            }
            var nested = Path.Combine(directory.FullName, "scripts", "Measure-PublishBudget.ps1");
            if (File.Exists(nested))
            {
                return nested;
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Could not locate scripts/Measure-PublishBudget.ps1 above the test assembly.");
    }

    private string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "viu-budget-tests", Guid.NewGuid().ToString("n"));
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

    private sealed record SampleMeasurement(
        long CompressedPublishSizeBytes,
        long RawPublishSizeBytes,
        int FileCount,
        long? BaselineBytes);
}
