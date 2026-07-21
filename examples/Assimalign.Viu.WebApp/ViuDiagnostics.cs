using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Browser;

// The ?diagnostics=1 mode: runs the [V01.01.04.01] handle-lifecycle stress check and the
// int-handle vs JSObject marshaling benchmark that feeds the ADR
// (libraries/Assimalign.Viu.Browser/docs/ADR-0001-interop-marshaling.md), then renders the
// report into #app. Example-level tooling: numbers are indicative dev-build measurements; the
// tracked benchmark suite is [V01.01.11.04].
[SupportedOSPlatform("browser")]
internal static class ViuDiagnostics
{
    private const int StressIterations = 100;
    private const int CreationOperations = 3_000;
    private const int TextOperations = 10_000;
    private const int AttributeOperations = 10_000;
    private const int TreeCount = 100;
    private const int TreeChildren = 30;

    internal static async Task RunAsync(int rootHandle)
    {
        await JSHost.ImportAsync("benchmark", "/benchmark.js");
        var renderer = BrowserRuntime.CreateRenderer();
        var report = new StringBuilder();
        report.AppendLine("VIU RUNTIME-DOM DIAGNOSTICS");
        report.AppendLine($"UTC: {DateTime.UtcNow:yyyy-MM-dd HH:mm}");
        report.AppendLine();

        RunHandleLifecycleStress(renderer, rootHandle, report);
        RunApplicationLifecycleStress(rootHandle, report);
        RunApplicationLifecycleStress(rootHandle, report, useCommandBuffer: true);
        RunMarshalingBenchmark(report);

        renderer.Render(
            VirtualNodeFactory.Element(
                "pre",
                VirtualNodeFactory.Properties(("style", "text-align:left;white-space:pre;font-size:13px;line-height:1.5")),
                report.ToString()),
            rootHandle);
    }

    private static void RunHandleLifecycleStress(Renderer<int> renderer, int rootHandle, StringBuilder report)
    {
        // The [V01.01.04.01] lifecycle criterion: N mount/unmount cycles of a large tree with
        // listeners must return the JS registries and the C# listener map to baseline.
        var baseline = BrowserRuntime.GetRegistryDiagnostics();
        var peakNodes = 0;
        for (var iteration = 0; iteration < StressIterations; iteration++)
        {
            renderer.Render(BuildLargeTree(iteration), rootHandle);
            var during = BrowserRuntime.GetRegistryDiagnostics();
            peakNodes = Math.Max(peakNodes, during.JsNodes);
            renderer.Render(null, rootHandle);
        }
        var after = BrowserRuntime.GetRegistryDiagnostics();
        var passed = after == baseline;
        report.AppendLine("HANDLE LIFECYCLE STRESS");
        report.AppendLine($"  cycles: {StressIterations} x ~{1 + (3 * 10)}-element tree with listeners");
        report.AppendLine($"  baseline: nodes={baseline.JsNodes} listenerMaps={baseline.JsListenerMaps} dotnetListeners={baseline.DotnetListeners}");
        report.AppendLine($"  peak:     nodes={peakNodes}");
        report.AppendLine($"  after:    nodes={after.JsNodes} listenerMaps={after.JsListenerMaps} dotnetListeners={after.DotnetListeners}");
        report.AppendLine($"  result:   {(passed ? "PASS - registries returned to baseline" : "FAIL - leak detected")}");
        report.AppendLine();
    }

    private static void RunApplicationLifecycleStress(int rootHandle, StringBuilder report, bool useCommandBuffer = false)
    {
        // The [V01.01.04.04] criterion: CreateApp/Mount/Unmount cycles — with component
        // lifecycles, props, emits, and timers — return the bridge registry to its pre-mount
        // baseline. Run once in direct mode and once with the interop command buffer
        // ([V01.01.04.05]): buffered mode must clean up handles and listeners identically — every
        // create/insert/remove flows through the batched apply, and released handles come back from
        // the single apply call to purge the invoker registry.
        var baseline = BrowserRuntime.GetRegistryDiagnostics();
        var peakNodes = 0;
        for (var iteration = 0; iteration < 25; iteration++)
        {
            var application = BrowserRuntime.CreateApp(new StopwatchApplication(), useCommandBuffer: useCommandBuffer);
            application.Mount(rootHandle);
            var during = BrowserRuntime.GetRegistryDiagnostics();
            peakNodes = Math.Max(peakNodes, during.JsNodes);
            application.Unmount();
        }
        var after = BrowserRuntime.GetRegistryDiagnostics();
        var passed = after == baseline;
        report.AppendLine($"APPLICATION LIFECYCLE STRESS (CreateApp/Mount/Unmount, {(useCommandBuffer ? "BUFFERED" : "direct")} mode)");
        report.AppendLine($"  cycles: 25 x component tree (root + child, listeners, timers)");
        report.AppendLine($"  baseline: nodes={baseline.JsNodes} listenerMaps={baseline.JsListenerMaps} dotnetListeners={baseline.DotnetListeners}");
        report.AppendLine($"  peak:     nodes={peakNodes}");
        report.AppendLine($"  after:    nodes={after.JsNodes} listenerMaps={after.JsListenerMaps} dotnetListeners={after.DotnetListeners}");
        report.AppendLine($"  result:   {(passed ? "PASS - registry returned to pre-mount baseline" : "FAIL - leak detected")}");
        report.AppendLine();
    }

    private static VirtualNode BuildLargeTree(int iteration)
    {
        var rows = new VirtualNode?[10];
        for (var index = 0; index < rows.Length; index++)
        {
            rows[index] = VirtualNodeFactory.Element(
                "div",
                VirtualNodeFactory.Properties(("class", "row"), ("data-iteration", iteration)),
                VirtualNodeFactory.Element("span", $"Row {index}"),
                VirtualNodeFactory.Element(
                    "button",
                    VirtualNodeFactory.Properties(("onClick", (Action)(static () => { })), ("type", "button")),
                    VirtualNodeFactory.Text("Press")),
                VirtualNodeFactory.Element(
                    "i",
                    VirtualNodeFactory.Properties(("style", "color:gray")),
                    $"cycle {iteration}"));
        }
        return VirtualNodeFactory.Element("div", null, rows);
    }

    private static void RunMarshalingBenchmark(StringBuilder report)
    {
        report.AppendLine("MARSHALING STRATEGY BENCHMARK (int handle vs JSObject proxy)");
        report.AppendLine($"  mixes: creation+teardown x{CreationOperations}, text x{TextOperations}, attribute x{AttributeOperations}, tree build+teardown {TreeCount}x{TreeChildren}");

        // Warm-up both paths so first-call interop setup is excluded.
        RunCreationMixWithHandles(50);
        RunCreationMixWithObjects(50);

        Row(report, "creation+teardown", CreationOperations * 2,
            Measure(static () => RunCreationMixWithHandles(CreationOperations)),
            Measure(static () => RunCreationMixWithObjects(CreationOperations)));
        Row(report, "set element text", TextOperations,
            Measure(static () => RunTextMixWithHandles(TextOperations)),
            Measure(static () => RunTextMixWithObjects(TextOperations)));
        Row(report, "set attribute", AttributeOperations,
            Measure(static () => RunAttributeMixWithHandles(AttributeOperations)),
            Measure(static () => RunAttributeMixWithObjects(AttributeOperations)));
        Row(report, "tree build+teardown", TreeCount * (TreeChildren * 3 + 2),
            Measure(static () => RunTreeMixWithHandles(TreeCount, TreeChildren)),
            Measure(static () => RunTreeMixWithObjects(TreeCount, TreeChildren)));
        report.AppendLine();
    }

    private static double Measure(Action mix)
    {
        var stopwatch = Stopwatch.StartNew();
        mix();
        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }

    private static void Row(StringBuilder report, string name, int operations, double handleMilliseconds, double objectMilliseconds)
    {
        var handleMicroseconds = handleMilliseconds * 1000 / operations;
        var objectMicroseconds = objectMilliseconds * 1000 / operations;
        var ratio = objectMilliseconds / handleMilliseconds;
        report.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"  {name,-20} int-handle: {handleMilliseconds,8:F1} ms ({handleMicroseconds,6:F2} us/op)   jsobject: {objectMilliseconds,8:F1} ms ({objectMicroseconds,6:F2} us/op)   ratio: {ratio:F2}x"));
    }

    private static void RunCreationMixWithHandles(int operations)
    {
        for (var index = 0; index < operations; index++)
        {
            var handle = DiagnosticsInterop.CreateElementHandle("div", null);
            DiagnosticsInterop.RemoveHandle(handle);
        }
    }

    private static void RunCreationMixWithObjects(int operations)
    {
        for (var index = 0; index < operations; index++)
        {
            var element = DiagnosticsInterop.CreateElementObject("div");
            DiagnosticsInterop.RemoveObject(element);
            element.Dispose();
        }
    }

    private static void RunTextMixWithHandles(int operations)
    {
        var handle = DiagnosticsInterop.CreateElementHandle("div", null);
        for (var index = 0; index < operations; index++)
        {
            DiagnosticsInterop.SetElementTextHandle(handle, (index & 1) == 0 ? "tick" : "tock");
        }
        DiagnosticsInterop.RemoveHandle(handle);
    }

    private static void RunTextMixWithObjects(int operations)
    {
        var element = DiagnosticsInterop.CreateElementObject("div");
        for (var index = 0; index < operations; index++)
        {
            DiagnosticsInterop.SetElementTextObject(element, (index & 1) == 0 ? "tick" : "tock");
        }
        DiagnosticsInterop.RemoveObject(element);
        element.Dispose();
    }

    private static void RunAttributeMixWithHandles(int operations)
    {
        var handle = DiagnosticsInterop.CreateElementHandle("div", null);
        for (var index = 0; index < operations; index++)
        {
            DiagnosticsInterop.SetAttributeHandle(handle, "data-value", (index & 1) == 0 ? "a" : "b");
        }
        DiagnosticsInterop.RemoveHandle(handle);
    }

    private static void RunAttributeMixWithObjects(int operations)
    {
        var element = DiagnosticsInterop.CreateElementObject("div");
        for (var index = 0; index < operations; index++)
        {
            DiagnosticsInterop.SetAttributeObject(element, "data-value", (index & 1) == 0 ? "a" : "b");
        }
        DiagnosticsInterop.RemoveObject(element);
        element.Dispose();
    }

    private static void RunTreeMixWithHandles(int trees, int childrenPerTree)
    {
        for (var tree = 0; tree < trees; tree++)
        {
            var rootHandle = DiagnosticsInterop.CreateElementHandle("div", null);
            for (var child = 0; child < childrenPerTree; child++)
            {
                var childHandle = DiagnosticsInterop.CreateElementHandle("span", null);
                DiagnosticsInterop.SetElementTextHandle(childHandle, "x");
                DiagnosticsInterop.InsertHandle(rootHandle, childHandle, 0);
            }
            DiagnosticsInterop.RemoveHandle(rootHandle);
        }
    }

    private static void RunTreeMixWithObjects(int trees, int childrenPerTree)
    {
        for (var tree = 0; tree < trees; tree++)
        {
            var root = DiagnosticsInterop.CreateElementObject("div");
            for (var child = 0; child < childrenPerTree; child++)
            {
                var childElement = DiagnosticsInterop.CreateElementObject("span");
                DiagnosticsInterop.SetElementTextObject(childElement, "x");
                DiagnosticsInterop.InsertObject(root, childElement);
                childElement.Dispose();
            }
            DiagnosticsInterop.RemoveObject(root);
            root.Dispose();
        }
    }
}
