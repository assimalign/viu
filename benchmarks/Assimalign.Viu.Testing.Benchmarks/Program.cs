using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace Assimalign.Viu.Testing.Benchmarks;

/// <summary>
/// Entry point for the Viu performance benchmark suite ([V01.01.11.04], #88). Routes between three modes:
/// <list type="bullet">
/// <item><c>benchmarks [BenchmarkDotNet args]</c> — the wall-clock micro/meso timings (Release only).</item>
/// <item><c>interop [--variant …] [--baseline …] [--results …] [--gate]</c> — the deterministic
/// interop-crossing count harness and its regression gate (fast, runs in Debug).</item>
/// <item><c>browser</c> — prints the honest deferral status of the real-browser lane (#87).</item>
/// </list>
/// </summary>
public static class Program
{
    /// <summary>Dispatches on the first argument.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>0 on success, 1 on a tripped gate, 2 on a usage/configuration error.</returns>
    public static int Main(string[] args)
    {
        var command = args.Length > 0 ? args[0] : "help";
        switch (command)
        {
            case "benchmarks":
                return RunBenchmarks(args[1..]);
            case "interop":
                return RunInterop(args[1..]);
            case "browser":
                DeferredBrowserBenchmarks.PrintDeferralNotice(Console.Out);
                return 0;
            case "help":
            case "--help":
            case "-h":
                PrintUsage(Console.Out);
                return 0;
            default:
                Console.Error.WriteLine(FormattableString.Invariant($"Unknown command: {command}"));
                PrintUsage(Console.Error);
                return 2;
        }
    }

    private static int RunBenchmarks(string[] benchmarkArgs)
    {
        // --smoke is our own flag (a fast ShortRun); everything else passes through to BenchmarkDotNet.
        var quick = false;
        var passthrough = new List<string>(benchmarkArgs.Length);
        foreach (var argument in benchmarkArgs)
        {
            if (argument == "--smoke")
            {
                quick = true;
            }
            else
            {
                passthrough.Add(argument);
            }
        }

        if (passthrough.Count == 0)
        {
            // BenchmarkDotNet with no arguments opens an interactive menu that reads stdin, which hangs in
            // CI. Require an explicit filter instead (e.g. `benchmarks --filter *`).
            Console.Error.WriteLine("The 'benchmarks' command needs a filter, e.g. --filter * or --list flat.");
            Console.Error.WriteLine("A short smoke run: benchmarks --smoke --filter *ReactivityBenchmarks*");
            return 2;
        }

        // The repo's central source-generator/analyzer MSBuild wiring breaks BenchmarkDotNet's default
        // toolchain: it fails to build the auto-generated boilerplate project nested under the repo tree
        // (MSB3030 on the generator DLL). Run in-process instead. Trade-off: less per-case isolation than a
        // child-process toolchain — acceptable because these wall-clock numbers are informational (the
        // gated metric is the deterministic interop count, not time), and per-case [GlobalSetup]/
        // [GlobalCleanup] still re-establishes the reactivity/scheduler state each case depends on.
        var job = (quick ? Job.ShortRun : Job.Default).WithToolchain(InProcessEmitToolchain.Instance);
        var config = ManualConfig.Create(DefaultConfig.Instance).AddJob(job);
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(passthrough.ToArray(), config);
        return 0;
    }

    private static int RunInterop(string[] interopArgs)
    {
        var variant = ScenarioVariant.Optimized;
        var baselinePath = DefaultBaselinePath();
        string? resultsPath = null;
        var gate = false;

        for (var index = 0; index < interopArgs.Length; index++)
        {
            var argument = interopArgs[index];
            switch (argument)
            {
                case "--variant":
                    if (!TryTakeValue(interopArgs, ref index, out var variantText)
                        || !Enum.TryParse(variantText, ignoreCase: true, out variant))
                    {
                        Console.Error.WriteLine("--variant requires Optimized or KeylessBypass.");
                        return 2;
                    }
                    break;
                case "--baseline":
                    if (!TryTakeValue(interopArgs, ref index, out baselinePath!))
                    {
                        Console.Error.WriteLine("--baseline requires a path.");
                        return 2;
                    }
                    break;
                case "--results":
                    if (!TryTakeValue(interopArgs, ref index, out resultsPath!))
                    {
                        Console.Error.WriteLine("--results requires a path.");
                        return 2;
                    }
                    break;
                case "--gate":
                    gate = true;
                    break;
                default:
                    Console.Error.WriteLine(FormattableString.Invariant($"Unknown interop option: {argument}"));
                    return 2;
            }
        }

        var measured = InteropCountHarness.MeasureAll(variant);

        if (baselinePath is not null && File.Exists(baselinePath))
        {
            var baseline = InteropCountBaseline.Load(baselinePath);
            var comparison = baseline.Compare(measured);
            InteropCountReport.WriteComparison(Console.Out, measured, comparison, variant);
            if (resultsPath is not null)
            {
                InteropCountReport.WriteResultsJson(resultsPath, InteropCountReport.BuildDocument(measured, variant, comparison.Passed));
            }
            return gate && !comparison.Passed ? 1 : 0;
        }

        // No baseline present: seed mode. Print the raw totals so the reviewed manifest can be authored,
        // but refuse to gate — a gate with no baseline is not a gate.
        if (gate)
        {
            Console.Error.WriteLine(FormattableString.Invariant($"--gate requires a baseline; none found at {baselinePath}."));
            return 2;
        }
        PrintRawResults(Console.Out, measured, variant);
        if (resultsPath is not null)
        {
            InteropCountReport.WriteResultsJson(resultsPath, InteropCountReport.BuildDocument(measured, variant, withinBaseline: null));
        }
        return 0;
    }

    private static void PrintRawResults(TextWriter writer, IReadOnlyList<InteropCountResult> results, ScenarioVariant variant)
    {
        writer.WriteLine();
        writer.WriteLine(FormattableString.Invariant($"Viu interop-count seed (variant={variant}); no baseline — authoring numbers below."));
        writer.WriteLine(new string('=', 78));
        var header = string.Format(
            CultureInfo.InvariantCulture,
            "{0,-20} {1,10} {2,12} {3,8} {4,8} {5,8} {6,8}",
            "Scenario", "Total", "Structural", "Create", "SetText", "Patch", "Remove");
        writer.WriteLine(header);
        writer.WriteLine(new string('-', header.Length));
        foreach (var result in results)
        {
            writer.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0,-20} {1,10} {2,12} {3,8} {4,8} {5,8} {6,8}",
                result.Name,
                result.TotalOperationCount,
                result.StructuralOperationCount,
                result.CreateElementCount,
                result.SetElementTextCount + result.SetTextCount,
                result.PatchPropertyCount,
                result.RemoveCount));
        }
    }

    private static string DefaultBaselinePath()
        => Path.Combine(AppContext.BaseDirectory, "baselines", "InteropCounts.json");

    private static bool TryTakeValue(string[] args, ref int index, out string value)
    {
        if (index + 1 < args.Length)
        {
            value = args[++index];
            return true;
        }
        value = "";
        return false;
    }

    private static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("Viu performance benchmark suite ([V01.01.11.04], #88)");
        writer.WriteLine();
        writer.WriteLine("Usage: Assimalign.Viu.Testing.Benchmarks <command> [options]");
        writer.WriteLine();
        writer.WriteLine("Commands:");
        writer.WriteLine("  benchmarks [args]   Wall-clock micro/meso timings via BenchmarkDotNet (build Release).");
        writer.WriteLine("      --smoke         Fast ShortRun (fewer iterations) for a quick end-to-end check.");
        writer.WriteLine("                      Smoke: benchmarks --smoke --filter *ReactivityBenchmarks*");
        writer.WriteLine("  interop [options]   Deterministic interop-crossing count harness + regression gate.");
        writer.WriteLine("      --variant V     Optimized (default) or KeylessBypass.");
        writer.WriteLine("      --baseline P    Baseline manifest (default: baselines/InteropCounts.json beside the app).");
        writer.WriteLine("      --results P     Write machine-readable results JSON to P.");
        writer.WriteLine("      --gate          Exit 1 if any scenario exceeds its reviewed baseline.");
        writer.WriteLine("  browser             Print the deferred real-browser lane status (#87).");
    }
}
