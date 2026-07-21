using System;
using System.IO;

namespace Assimalign.Viu.Testing.Benchmarks;

/// <summary>
/// The wired-but-deferred real-browser benchmark lane. Three axes can only be measured in a real headless
/// browser against a published, trimmed WASM benchmark app: wall-clock scenario time including JS-interop
/// initialization, the live count of command-buffer boundary crossings (buffered vs bypassed), and the
/// per-assembly published payload size. All three depend on the Playwright end-to-end harness
/// ([V01.01.11.03], #87) for the shared publish/serve/drive plumbing, which is not built yet.
/// <para>
/// Nothing here fakes those numbers: a pure-.NET timer is not a substitute (interop init cost is part of
/// what is measured), so this lane is skipped by default and turns on — exactly like the startup gate in
/// <c>budget-gates.yml</c> — once the harness lands, by setting the repository variable
/// <see cref="EnableVariableName"/> to <c>true</c>. Until then <see cref="PrintDeferralNotice"/> reports
/// the honest status and the CI job is a no-op placeholder.
/// </para>
/// </summary>
public static class DeferredBrowserBenchmarks
{
    /// <summary>The repository variable that activates the browser lane once #87 lands.</summary>
    public const string EnableVariableName = "ENABLE_BROWSER_BENCHMARKS";

    /// <summary>Writes the honest deferral status to <paramref name="writer"/>.</summary>
    /// <param name="writer">The destination writer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> is null.</exception>
    public static void PrintDeferralNotice(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteLine("Real-browser benchmarks are deferred pending the Playwright end-to-end harness");
        writer.WriteLine("([V01.01.11.03], #87), which owns the shared publish/serve/drive plumbing.");
        writer.WriteLine();
        writer.WriteLine("When that harness lands, this lane will, against a published trimmed WASM app:");
        writer.WriteLine("  1. measure per-scenario wall-clock time in headless Chromium/Firefox/WebKit");
        writer.WriteLine("     (navigation start -> settled DOM), including JS-interop initialization;");
        writer.WriteLine("  2. count live command-buffer boundary crossings per scenario (the metric the");
        writer.WriteLine("     command buffer exists to drive down) and fail a time-neutral crossing regression;");
        writer.WriteLine("  3. record the published payload size, compressed and uncompressed, per assembly.");
        writer.WriteLine();
        writer.WriteLine(FormattableString.Invariant(
            $"Enable by setting the repository variable {EnableVariableName}=true once #87 is available."));
        writer.WriteLine("A pure-.NET timer is NOT an acceptable substitute and is never faked here.");
    }
}
