using System;
using System.IO;

namespace Assimalign.Viu.Testing.Benchmarks;

/// <summary>
/// The wired-but-deferred real-browser benchmark lane. The packaged-consumer Playwright harness
/// ([V01.01.11.03], #87) now supplies shared publish, serve, browser-matrix, and diagnostic plumbing.
/// The benchmark-specific wall-clock, live command-buffer crossing, and per-assembly payload
/// instrumentation remains separate work under [V01.01.11.04], #88.
/// <para>
/// Nothing here fakes those numbers: a pure-.NET timer is not a substitute (interop init cost is part of
/// what is measured), so this lane remains skipped until #88 connects those measurements to the harness.
/// <see cref="PrintDeferralNotice"/> reports that residual scope and the CI job stays a no-op placeholder.
/// </para>
/// </summary>
public static class DeferredBrowserBenchmarks
{
    /// <summary>The repository variable that activates the browser lane after #88 instrumentation lands.</summary>
    public const string EnableVariableName = "ENABLE_BROWSER_BENCHMARKS";

    /// <summary>Writes the honest deferral status to <paramref name="writer"/>.</summary>
    /// <param name="writer">The destination writer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> is null.</exception>
    public static void PrintDeferralNotice(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteLine("The Playwright end-to-end harness ([V01.01.11.03], #87) is available.");
        writer.WriteLine("Real-browser benchmark instrumentation remains deferred under [V01.01.11.04], #88.");
        writer.WriteLine();
        writer.WriteLine("The remaining #88 work must connect a published trimmed benchmark app to:");
        writer.WriteLine("  1. measure per-scenario wall-clock time in headless Chromium/Firefox/WebKit");
        writer.WriteLine("     (navigation start -> settled DOM), including JS-interop initialization;");
        writer.WriteLine("  2. count live command-buffer boundary crossings per scenario (the metric the");
        writer.WriteLine("     command buffer exists to drive down) and fail a time-neutral crossing regression;");
        writer.WriteLine("  3. record the published payload size, compressed and uncompressed, per assembly.");
        writer.WriteLine();
        writer.WriteLine(FormattableString.Invariant(
            $"Enable by setting the repository variable {EnableVariableName}=true after #88 lands."));
        writer.WriteLine("A pure-.NET timer is NOT an acceptable substitute and is never faked here.");
    }
}
