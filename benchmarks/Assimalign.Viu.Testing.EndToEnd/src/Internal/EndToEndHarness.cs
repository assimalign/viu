using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Playwright;

namespace Assimalign.Viu.Testing.EndToEnd;

internal sealed class EndToEndHarness
{
    private const string ManagedHotReloadCompletionMessage =
        "C# and Razor changes applied";
    private const string StaticAssetHotReloadCompletionMessage =
        "Static asset changes applied";
    private static readonly TimeSpan AssertionTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan HotReloadAssertionTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan HotReloadNoOperationObservationWindow =
        TimeSpan.FromSeconds(3);
    private readonly HarnessOptions _options;
    private readonly List<ScenarioResult> _results = [];

    internal EndToEndHarness(HarnessOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    internal async Task<int> RunAsync()
    {
        Directory.CreateDirectory(_options.ArtifactDirectory);
        if (_options.HotReloadProjectPath is not null)
        {
            await RunHotReloadLaneAsync(
                _options.HotReloadProjectPath,
                _options.HotReloadViuVersion!);
        }
        else
        {
            await RunPublishedFixtureLaneAsync();
        }

        await WriteResultSummaryAsync();
        int failed = _results.Count(result => !result.Succeeded);
        Console.WriteLine(
            $"End-to-end summary: {_results.Count - failed} passed, {failed} failed, {_results.Count} total.");
        foreach (ScenarioResult result in _results)
        {
            Console.WriteLine(
                $"  [{(result.Succeeded ? "PASS" : "FAIL")}] "
                + $"{result.BrowserEngine}/{result.Scenario} "
                + $"({result.DurationMilliseconds.ToString("F1", CultureInfo.InvariantCulture)} ms)");
            if (!result.Succeeded)
            {
                Console.WriteLine($"    {result.Failure}");
                Console.WriteLine($"    screenshot: {result.ScreenshotPath}");
                Console.WriteLine($"    trace: {result.TracePath}");
            }
        }

        return failed == 0 ? 0 : 1;
    }

    private async Task RunPublishedFixtureLaneAsync()
    {
        await using StaticWebServer browserServer =
            StaticWebServer.Start(_options.BrowserRootDirectory!);
        await using StaticWebServer hydrationServer =
            StaticWebServer.Start(_options.HydrationRootDirectory!);
        Console.WriteLine($"Browser fixture: {browserServer.Address}");
        Console.WriteLine($"Hydration fixture: {hydrationServer.Address}");

        using IPlaywright playwright = await Playwright.CreateAsync();
        foreach (BrowserEngine browserEngine in _options.BrowserEngines)
        {
            await RunBrowserEngineAsync(
                playwright,
                browserEngine,
                browserServer.Address,
                hydrationServer.Address);
        }
    }

    private async Task RunHotReloadLaneAsync(
        string projectPath,
        string viuVersion)
    {
        await RunVisualStudioHotReloadScenarioAsync(projectPath, viuVersion);
        await RunNaiveInvocationHotReloadScenarioAsync(projectPath, viuVersion);

        string chromiumArtifactDirectory = Path.Combine(
            _options.ArtifactDirectory,
            "chromium");
        await using HotReloadWatchSession session = new(
            projectPath,
            viuVersion,
            chromiumArtifactDirectory);
        await session.StartAsync();
        Console.WriteLine($"Hot-reload fixture: {session.Address}");

        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions
            {
                Headless = !_options.Headed,
            });
        await using IBrowserContext context = await browser.NewContextAsync(
            new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize
                {
                    Width = 1280,
                    Height = 720,
                },
            });
        List<string> browserErrors = [];
        List<string> requestFailures = [];
        IPage page = await context.NewPageAsync();
        page.Console += (_, message) =>
        {
            if (string.Equals(message.Type, "error", StringComparison.OrdinalIgnoreCase))
            {
                lock (browserErrors)
                {
                    browserErrors.Add($"console: {message.Text}");
                }
            }
        };
        page.PageError += (_, message) =>
        {
            lock (browserErrors)
            {
                browserErrors.Add($"page: {message}");
            }
        };
        page.RequestFailed += (_, request) =>
        {
            if (IsExpectedHotReloadRequestAbort(request))
            {
                return;
            }

            lock (requestFailures)
            {
                requestFailures.Add(
                    $"{request.Method} {request.Url}: {request.Failure}");
            }
        };

        // [V01.01.06.14], #350, [SFC-CG-4]: structural template edits keep the
        // generated member surface stable and apply sequentially to one connected Mono-WASM
        // document. A fresh page would boot the unchanged on-disk assembly and miss earlier deltas.
        await RunConnectedHotReloadScenarioAsync(
            context,
            page,
            BrowserEngine.Chromium,
            "packaged-vue-watch-structural-add-delta",
            browserErrors,
            requestFailures,
            connectedPage => RunStructuralAddScenarioAsync(connectedPage, session));
        await RunConnectedHotReloadScenarioAsync(
            context,
            page,
            BrowserEngine.Chromium,
            "packaged-vue-watch-structural-remove-delta",
            browserErrors,
            requestFailures,
            connectedPage => RunStructuralRemoveScenarioAsync(connectedPage, session));
        await RunConnectedHotReloadScenarioAsync(
            context,
            page,
            BrowserEngine.Chromium,
            "packaged-vue-watch-structural-v-if-delta",
            browserErrors,
            requestFailures,
            connectedPage => RunStructuralVIfScenarioAsync(connectedPage, session));
        // [V01.01.06.14], #350: a newly watched component is delivered through
        // the runtime's NewTypeDefinition capability without restarting the application.
        await RunConnectedHotReloadScenarioAsync(
            context,
            page,
            BrowserEngine.Chromium,
            "packaged-vue-watch-new-file-new-type-definition-delta",
            browserErrors,
            requestFailures,
            connectedPage => RunNewTypeDefinitionScenarioAsync(connectedPage, session));
        await RunConnectedHotReloadScenarioAsync(
            context,
            page,
            BrowserEngine.Chromium,
            "packaged-vue-watch-css-and-remount",
            browserErrors,
            requestFailures,
            connectedPage => RunHotReloadScenarioAsync(connectedPage, session));
        await RunConnectedHotReloadScenarioAsync(
            context,
            page,
            BrowserEngine.Chromium,
            "packaged-vue-watch-script-signature-restart-reload",
            browserErrors,
            requestFailures,
            connectedPage => RunScriptSignatureRestartScenarioAsync(connectedPage, session));
        // [V01.01.12.30.04], #355: run generated-asset creation and retirement after the
        // existing rude-edit restart so all pre-existing scenarios retain their exact sequence.
        await RunConnectedHotReloadScenarioAsync(
            context,
            page,
            BrowserEngine.Chromium,
            "packaged-vue-watch-utility-css-new-class",
            browserErrors,
            requestFailures,
            connectedPage => RunUtilityCssNewClassScenarioAsync(connectedPage, session));
        await RunConnectedHotReloadScenarioAsync(
            context,
            page,
            BrowserEngine.Chromium,
            "packaged-vue-watch-utility-css-last-source-removal",
            browserErrors,
            requestFailures,
            connectedPage => RunUtilityCssLastSourceRemovalScenarioAsync(connectedPage, session));
        await session.StopAsync();
    }

    // [V01.01.12.30.05], #357: this must run before any dotnet-watch process. The freshly staged
    // project then proves an ordinary Debug build emitted the configuration consumed by RunHost.
    private async Task RunVisualStudioHotReloadScenarioAsync(
        string projectPath,
        string viuVersion)
    {
        string artifactDirectory = Path.Combine(
            _options.ArtifactDirectory,
            "chromium",
            "visual-studio");
        await using VisualStudioBrowserRefreshStubServer refreshServer =
            await VisualStudioBrowserRefreshStubServer.StartAsync();
        await using VisualStudioHotReloadSession session = new(
            projectPath,
            viuVersion,
            artifactDirectory);
        byte[] originalVisualStudioSourceContent = await File.ReadAllBytesAsync(
            session.VisualStudioSourcePath);
        try
        {
            await session.StartAsync(refreshServer);
            Console.WriteLine(
                $"Visual Studio-shaped hot-reload fixture: {session.Address}");

            using IPlaywright playwright = await Playwright.CreateAsync();
            await using IBrowser browser = await playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions
                {
                    Headless = !_options.Headed,
                });
            await RunScenarioAsync(
                browser,
                BrowserEngine.Chromium,
                "packaged-vue-run-host-visual-studio-generated-assets",
                page => RunVisualStudioGeneratedAssetScenarioAsync(
                    page,
                    session,
                    refreshServer));
        }
        finally
        {
            try
            {
                await session.StopAsync();
            }
            finally
            {
                await File.WriteAllBytesAsync(
                    session.VisualStudioSourcePath,
                    originalVisualStudioSourceContent);
            }
        }
    }

    private static async Task RunVisualStudioGeneratedAssetScenarioAsync(
        IPage page,
        VisualStudioHotReloadSession session,
        VisualStudioBrowserRefreshStubServer refreshServer)
    {
        await page.AddInitScriptAsync(
            """
            (() => {
                const NativeWebSocket = globalThis.WebSocket;
                globalThis.__viuBrowserRefreshProbe = {
                    connections: [],
                    messages: []
                };
                globalThis.WebSocket = new Proxy(NativeWebSocket, {
                    construct(target, argumentsList) {
                        const socket = Reflect.construct(target, argumentsList, target);
                        const protocols = argumentsList.length < 2
                            ? []
                            : Array.isArray(argumentsList[1])
                                ? [...argumentsList[1]]
                                : [String(argumentsList[1])];
                        globalThis.__viuBrowserRefreshProbe.connections.push({
                            url: String(argumentsList[0]),
                            protocols,
                            socket
                        });
                        socket.addEventListener('message', event => {
                            if (typeof event.data === 'string') {
                                globalThis.__viuBrowserRefreshProbe.messages.push(event.data);
                            }
                        });
                        return socket;
                    }
                });
            })();
            """);
        await NavigateAsync(page, session.Address.AbsoluteUri);
        await RequireTextAsync(page, "hot-heading", "Hot reload template v1");
        await WaitUntilAsync(
            async () => await page
                .Locator("script[src*='aspnetcore-browser-refresh']")
                .CountAsync() == 1,
            "the SDK-injected BrowserRefresh client script",
            HotReloadAssertionTimeout);
        await WaitUntilAsync(
            async () => await page.EvaluateAsync<bool>(
                "() => globalThis.__viuBrowserRefreshProbe.connections"
                + ".some(connection => connection.socket.readyState === 1)"),
            "the browser to connect to the RunHost BrowserRefresh endpoint",
            HotReloadAssertionTimeout);
        await refreshServer.WaitForAuthenticatedConnectionAsync();

        string downstreamAddress = await page.EvaluateAsync<string>(
            "() => globalThis.__viuBrowserRefreshProbe.connections"
            + ".find(connection => connection.socket.readyState === 1)?.url ?? ''");
        Require(
            !string.IsNullOrEmpty(downstreamAddress),
            "The BrowserRefresh client did not expose its connected endpoint.");
        Require(
            Uri.TryCreate(downstreamAddress, UriKind.Absolute, out Uri? downstreamUri)
                && downstreamUri.IsLoopback,
            "The RunHost BrowserRefresh endpoint was not bound to loopback.");
        Require(
            !string.Equals(
                downstreamAddress,
                refreshServer.Address.AbsoluteUri,
                StringComparison.OrdinalIgnoreCase),
            "The browser connected directly to the Visual Studio stub instead of the RunHost bridge.");
        int downstreamProtocolCount = await page.EvaluateAsync<int>(
            "() => globalThis.__viuBrowserRefreshProbe.connections"
            + ".find(connection => connection.socket.readyState === 1)?.protocols.length ?? 0");
        Require(
            downstreamProtocolCount == 1,
            "The RunHost did not preserve Visual Studio's encrypted BrowserRefresh protocol for the page.");
        string downstreamProtocol = await page.EvaluateAsync<string>(
            "() => globalThis.__viuBrowserRefreshProbe.connections"
            + ".find(connection => connection.socket.readyState === 1)?.protocols[0] ?? ''");
        Require(
            !string.IsNullOrWhiteSpace(downstreamProtocol),
            "The page's Visual Studio BrowserRefresh protocol was empty.");

        await refreshServer.SendAsync("{\"type\":\"GetApplyUpdateCapabilities\"}");
        string capabilityResponse = await refreshServer.WaitForReceivedMessageAsync(
            message => !string.IsNullOrWhiteSpace(message));
        Require(
            !string.IsNullOrWhiteSpace(capabilityResponse),
            "The RunHost did not relay the browser's capability response upstream.");

        await RequireStylesheetRulesAsync(page, ".viu.css", minimumRuleCount: 1);
        await RequireStylesheetRulesAsync(page, ".utilities.css", minimumRuleCount: 1);
        await RequireComputedDisplayAsync(page, "hot-shell", "grid");
        string documentToken = await SetDocumentTokenAsync(page);
        await page
            .Locator("[data-testid='utility-style-probe']")
            .EvaluateAsync<bool>(
                "element => { element.classList.add('visual-studio-style-probe', 'opacity-50'); "
                + "return true; }");
        await RequireComputedStylePropertyAsync(
            page,
            "utility-style-probe",
            "border-left-width",
            "1px");
        await RequireComputedStylePropertyAsync(
            page,
            "utility-style-probe",
            "opacity",
            "1");

        byte[] originalComponentBundle = await File.ReadAllBytesAsync(
            session.ComponentBundlePath);
        byte[] originalUtilityBundle = await File.ReadAllBytesAsync(
            session.UtilityBundlePath);
        Require(
            !Encoding.UTF8.GetString(originalUtilityBundle).Contains(
                "opacity: 0.5;",
                StringComparison.Ordinal),
            "The utility bundle already contained the Visual Studio scenario's new class.");
        string componentStylesheetAddress = await ReadStylesheetAddressAsync(
            page,
            ".viu.css");
        string utilityStylesheetAddress = await ReadStylesheetAddressAsync(
            page,
            ".utilities.css");
        int componentUpdateCount = await CountStaticFileUpdatesAsync(
            page,
            "/EndToEndHotReloadApp.viu.css");
        int utilityUpdateCount = await CountStaticFileUpdatesAsync(
            page,
            "/EndToEndHotReloadApp.utilities.css");
        int completionCount = ReadCssCompletionCount(session.CssEventLogPath);

        await EditVisualStudioGeneratedAssetSourceAsync(
            session.VisualStudioSourcePath);
        await WaitForCssCompletionAsync(session, completionCount + 1);
        await WaitForStaticFileUpdateAsync(
            page,
            "/EndToEndHotReloadApp.viu.css",
            componentUpdateCount + 1);
        await WaitForStaticFileUpdateAsync(
            page,
            "/EndToEndHotReloadApp.utilities.css",
            utilityUpdateCount + 1);
        await RequireStylesheetAddressChangedAsync(
            page,
            ".viu.css",
            componentStylesheetAddress);
        await RequireStylesheetAddressChangedAsync(
            page,
            ".utilities.css",
            utilityStylesheetAddress);
        await RequireComputedStylePropertyAsync(
            page,
            "utility-style-probe",
            "border-left-width",
            "7px");
        await RequireComputedStylePropertyAsync(
            page,
            "utility-style-probe",
            "opacity",
            "0.5");
        byte[] changedComponentBundle = await File.ReadAllBytesAsync(
            session.ComponentBundlePath);
        byte[] changedUtilityBundle = await File.ReadAllBytesAsync(
            session.UtilityBundlePath);
        Require(
            !originalComponentBundle.SequenceEqual(changedComponentBundle),
            "The Visual Studio-shaped .viu style edit did not rewrite the component bundle.");
        Require(
            !originalUtilityBundle.SequenceEqual(changedUtilityBundle),
            "The Visual Studio-shaped .viu utility edit did not rewrite the utility bundle.");
        Require(
            Encoding.UTF8.GetString(changedUtilityBundle).Contains(
                "opacity: 0.5;",
                StringComparison.Ordinal),
            "The Visual Studio-shaped utility bundle does not contain the new opacity declaration.");
        await RequireDocumentTokenAsync(page, documentToken);
        session.RequireRunning();
    }

    // [V01.01.12.33], #356: exercise the invocation developers type before the explicit-runtime
    // session mutates the connected managed application through its established cumulative sequence.
    private async Task RunNaiveInvocationHotReloadScenarioAsync(
        string projectPath,
        string viuVersion)
    {
        string artifactDirectory = Path.Combine(
            _options.ArtifactDirectory,
            "chromium",
            "no-runtime");
        await using HotReloadWatchSession session = new(
            projectPath,
            viuVersion,
            artifactDirectory);
        byte[] originalMainSourceContent = await File.ReadAllBytesAsync(
            session.MainSourcePath);
        byte[] originalUtilityCandidateSourceContent = await File.ReadAllBytesAsync(
            session.UtilityCandidateSourcePath);
        try
        {
            await session.StartAsync(includeExplicitRuntimeIdentifier: false);
            Console.WriteLine($"No-runtime hot-reload fixture: {session.Address}");

            using IPlaywright playwright = await Playwright.CreateAsync();
            await using IBrowser browser = await playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions
                {
                    Headless = !_options.Headed,
                });
            await RunScenarioAsync(
                browser,
                BrowserEngine.Chromium,
                "packaged-vue-watch-no-runtime-css-and-utility",
                page => RunNaiveInvocationCssScenarioAsync(page, session));
        }
        finally
        {
            try
            {
                await session.StopAsync();
            }
            finally
            {
                await File.WriteAllBytesAsync(
                    session.MainSourcePath,
                    originalMainSourceContent);
                await File.WriteAllBytesAsync(
                    session.UtilityCandidateSourcePath,
                    originalUtilityCandidateSourceContent);
            }
        }
    }

    private static async Task RunStructuralAddScenarioAsync(
        IPage page,
        HotReloadWatchSession session)
    {
        await NavigateHotReloadAsync(page, session);
        await RequireTextAsync(page, "hot-heading", "Hot reload template v1");
        await RequireTextAsync(
            page,
            "hot-removable",
            "Structural removal baseline");
        string documentToken = await PrepareRemountProbeAsync(page);
        HotReloadProcessSnapshot snapshot = CaptureHotReloadProcessSnapshot(session);

        await ReplaceSourceTextAsync(
            session.MainSourcePath,
            "        <div data-testid=\"hot-removable\">Structural removal baseline</div>",
            "        <div data-testid=\"hot-added\">Structural add landed</div>"
            + Environment.NewLine
            + "        <div data-testid=\"hot-removable\">Structural removal baseline</div>");
        await RequireAcceptedManagedDeltaAsync(
            session,
            snapshot,
            "the structural element addition to apply as a managed delta");
        await RequireTextAsync(
            page,
            "hot-added",
            "Structural add landed",
            HotReloadAssertionTimeout);
        await RequireTextAsync(page, "hot-count", "0", HotReloadAssertionTimeout);
        await RequireDocumentTokenAsync(page, documentToken);
    }

    private static async Task RunStructuralRemoveScenarioAsync(
        IPage page,
        HotReloadWatchSession session)
    {
        await RequireTextAsync(page, "hot-heading", "Hot reload template v1");
        await RequireTextAsync(
            page,
            "hot-removable",
            "Structural removal baseline");
        await RequireTextAsync(page, "hot-added", "Structural add landed");
        string documentToken = await PrepareRemountProbeAsync(page);
        HotReloadProcessSnapshot snapshot = CaptureHotReloadProcessSnapshot(session);

        await ReplaceSourceTextAsync(
            session.MainSourcePath,
            "        <div data-testid=\"hot-removable\">Structural removal baseline</div>",
            string.Empty);
        await RequireAcceptedManagedDeltaAsync(
            session,
            snapshot,
            "the structural element removal to apply as a managed delta");
        await RequireAbsentAsync(page, "hot-removable", HotReloadAssertionTimeout);
        await RequireTextAsync(page, "hot-added", "Structural add landed");
        await RequireTextAsync(page, "hot-count", "0", HotReloadAssertionTimeout);
        await RequireDocumentTokenAsync(page, documentToken);
    }

    private static async Task RunStructuralVIfScenarioAsync(
        IPage page,
        HotReloadWatchSession session)
    {
        await RequireTextAsync(page, "hot-heading", "Hot reload template v1");
        await RequireAbsentAsync(page, "hot-conditional");
        string documentToken = await PrepareRemountProbeAsync(page);
        HotReloadProcessSnapshot snapshot = CaptureHotReloadProcessSnapshot(session);

        await ReplaceSourceTextAsync(
            session.MainSourcePath,
            "        <p data-testid=\"hot-count\">{{ Count }}</p>",
            "        <div data-testid=\"hot-conditional\" v-if=\"Count > 0\">"
            + "Conditional element landed</div>"
            + Environment.NewLine
            + "        <p data-testid=\"hot-count\">{{ Count }}</p>");
        await RequireAcceptedManagedDeltaAsync(
            session,
            snapshot,
            "the v-if structural addition to apply as a managed delta");
        await RequireTextAsync(page, "hot-count", "0", HotReloadAssertionTimeout);
        await RequireAbsentAsync(page, "hot-conditional", HotReloadAssertionTimeout);
        await RequireDocumentTokenAsync(page, documentToken);
        await page.Locator("[data-testid='hot-increment']").ClickAsync();
        await RequireTextAsync(page, "hot-count", "1");
        await RequireTextAsync(
            page,
            "hot-conditional",
            "Conditional element landed",
            HotReloadAssertionTimeout);
        await RequireDocumentTokenAsync(page, documentToken);
    }

    private static async Task RunNewTypeDefinitionScenarioAsync(
        IPage page,
        HotReloadWatchSession session)
    {
        await RequireTextAsync(page, "hot-heading", "Hot reload template v1");
        await RequireTextAsync(page, "hot-count", "1");
        string documentToken = await SetDocumentTokenAsync(page);
        await RequireDocumentTokenAsync(page, documentToken);
        await RequireTextAsync(
            page,
            "hot-conditional",
            "Conditional element landed");
        HotReloadProcessSnapshot snapshot = CaptureHotReloadProcessSnapshot(session);
        int fileReevaluationCount = session.CountOutputLinesContaining(
            "File addition triggered re-evaluation:");
        int newTypeDefinitionCapabilityCount = session.CountOutputLinesContaining(
            "NewTypeDefinition");
        int runtimeAppliedUpdateCount = session.CountOutputLinesContaining(
            "[Browser #1] Updates applied.");
        Require(
            !File.Exists(session.NewSourcePath),
            $"The new-type-definition probe already exists: {session.NewSourcePath}");

        string temporarySourcePath = Path.Combine(
            Path.GetTempPath(),
            Path.GetRandomFileName() + ".pending");
        try
        {
            await File.WriteAllTextAsync(
                temporarySourcePath,
                """
                <template>
                    <aside data-testid="new-type-probe">{{ Message }}</aside>
                </template>

                @script {
                    public string Message => "new type definition";
                }
                """,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporarySourcePath, session.NewSourcePath);
        }
        finally
        {
            File.Delete(temporarySourcePath);
        }
        await WaitUntilAsync(
            () => Task.FromResult(
                session.CountOutputLinesContaining(
                    "File addition triggered re-evaluation:") > fileReevaluationCount
                && session.CountOutputLinesContaining(
                    "NewTypeDefinition") > newTypeDefinitionCapabilityCount),
            "the new component file to re-evaluate with NewTypeDefinition capability",
            HotReloadAssertionTimeout);
        await RequireAcceptedManagedDeltaAsync(
            session,
            snapshot,
            "the new component type definition to apply as a managed delta");
        // The added file is the only semantic change in this batch. The browser acknowledgement
        // therefore pins runtime application of its NewTypeDefinition delta, not merely capability
        // advertisement by the watch host.
        Require(
            session.CountOutputLinesContaining("[Browser #1] Updates applied.")
                > runtimeAppliedUpdateCount,
            "The browser runtime did not acknowledge the new component type definition.");
        await RequireTextAsync(page, "hot-count", "1");
        await RequireTextAsync(
            page,
            "hot-conditional",
            "Conditional element landed");
        await RequireDocumentTokenAsync(page, documentToken);
    }

    private static async Task RunHotReloadScenarioAsync(
        IPage page,
        HotReloadWatchSession session)
    {
        await RequireTextAsync(page, "hot-heading", "Hot reload template v1");
        await RequireTextAsync(page, "hot-count", "1");
        await RequireStylesheetRulesAsync(
            page,
            ".viu.css",
            minimumRuleCount: 1);
        await RequireComputedDisplayAsync(page, "hot-shell", "grid");

        string documentToken = await page.EvaluateAsync<string>(
            "() => { const token = `${Date.now()}-${Math.random()}`; "
            + "globalThis.__viuHotReloadDocumentToken = token; return token; }");
        await page.Locator("[data-testid='hot-increment']").ClickAsync();
        await RequireTextAsync(page, "hot-count", "2");

        byte[] originalBundleContent = await File.ReadAllBytesAsync(
            session.ComponentBundlePath);
        int completionCount = ReadCssCompletionCount(session.CssEventLogPath);
        await ReplaceSourceTextAsync(
            session.MainSourcePath,
            "display: grid;",
            "display: flex;");
        await WaitForCssCompletionAsync(session, completionCount + 1);
        await RequireComputedDisplayAsync(page, "hot-shell", "flex");
        byte[] changedBundleContent = await File.ReadAllBytesAsync(
            session.ComponentBundlePath);
        Require(
            !originalBundleContent.SequenceEqual(changedBundleContent),
            "The component style edit did not rewrite the generated bundle.");
        await RequireTextAsync(page, "hot-count", "2");
        await RequireDocumentTokenAsync(page, documentToken);

        await Task.Delay(HotReloadNoOperationObservationWindow);
        DateTime bundleWriteTime = File.GetLastWriteTimeUtc(
            session.ComponentBundlePath);
        string stylesheetAddress = await ReadStylesheetAddressAsync(
            page,
            ".viu.css");
        int managedUpdateCount = session.CountOutputLinesContaining(
            ManagedHotReloadCompletionMessage);
        int staticAssetUpdateCount = session.CountOutputLinesContaining(
            StaticAssetHotReloadCompletionMessage);
        completionCount = ReadCssCompletionCount(session.CssEventLogPath);
        await RewriteSourceWithoutContentChangeAsync(session.MainSourcePath);
        await WaitForCssCompletionAsync(session, completionCount + 1);
        await Task.Delay(HotReloadNoOperationObservationWindow);
        Require(
            (await File.ReadAllBytesAsync(session.ComponentBundlePath))
                .SequenceEqual(changedBundleContent),
            "A no-content-change component-style update changed the generated bundle.");
        Require(
            File.GetLastWriteTimeUtc(session.ComponentBundlePath) == bundleWriteTime,
            "A no-content-change component-style update rewrote the generated bundle.");
        Require(
            string.Equals(
                await ReadStylesheetAddressAsync(page, ".viu.css"),
                stylesheetAddress,
                StringComparison.Ordinal),
            "A no-content-change component-style update replaced the browser stylesheet link.");
        Require(
            session.CountOutputLinesContaining(ManagedHotReloadCompletionMessage)
                == managedUpdateCount,
            "A no-content-change component-style update triggered a managed hot-reload update.");
        Require(
            session.CountOutputLinesContaining(StaticAssetHotReloadCompletionMessage)
                == staticAssetUpdateCount,
            "A no-content-change component-style update triggered a static-asset update.");
        await RequireTextAsync(page, "hot-count", "2");
        await RequireDocumentTokenAsync(page, documentToken);

        HotReloadProcessSnapshot templateSnapshot = CaptureHotReloadProcessSnapshot(session);
        await ReplaceSourceTextAsync(
            session.MainSourcePath,
            "Hot reload template v1",
            "Hot reload template v2");
        await RequireAcceptedManagedDeltaAsync(
            session,
            templateSnapshot,
            "the existing text-edit scenario to apply as a managed delta");
        await RequireTextAsync(
            page,
            "hot-heading",
            "Hot reload template v2",
            HotReloadAssertionTimeout);
        await RequireTextAsync(
            page,
            "hot-count",
            "0",
            HotReloadAssertionTimeout);
        await RequireDocumentTokenAsync(page, documentToken);

        await page.Locator("[data-testid='hot-increment']").ClickAsync();
        await RequireTextAsync(page, "hot-count", "1");
        HotReloadProcessSnapshot scriptBodySnapshot = CaptureHotReloadProcessSnapshot(session);
        await ReplaceSourceTextAsync(
            session.MainSourcePath,
            "CountReference.Value++;",
            "CountReference.Value += 2;");
        await RequireAcceptedManagedDeltaAsync(
            session,
            scriptBodySnapshot,
            "the existing script-body scenario to apply as a managed delta");
        await RequireTextAsync(
            page,
            "hot-count",
            "0",
            HotReloadAssertionTimeout);
        await RequireDocumentTokenAsync(page, documentToken);
        await page.Locator("[data-testid='hot-increment']").ClickAsync();
        await RequireTextAsync(
            page,
            "hot-count",
            "2",
            HotReloadAssertionTimeout);
        await RequireDocumentTokenAsync(page, documentToken);
        session.RequireRunning();
    }

    private static async Task RunNaiveInvocationCssScenarioAsync(
        IPage page,
        HotReloadWatchSession session)
    {
        await NavigateHotReloadAsync(page, session);
        await RequireTextAsync(page, "hot-heading", "Hot reload template v1");
        await RequireStylesheetRulesAsync(page, ".viu.css", minimumRuleCount: 1);
        await RequireStylesheetRulesAsync(page, ".utilities.css", minimumRuleCount: 1);
        await RequireComputedDisplayAsync(page, "hot-shell", "grid");

        string documentToken = await SetDocumentTokenAsync(page);
        byte[] originalComponentBundle = await File.ReadAllBytesAsync(
            session.ComponentBundlePath);
        string componentStylesheetAddress = await ReadStylesheetAddressAsync(
            page,
            ".viu.css");
        int staticAssetUpdateCount = session.CountOutputLinesContaining(
            StaticAssetHotReloadCompletionMessage);
        int completionCount = ReadCssCompletionCount(session.CssEventLogPath);

        await ReplaceSourceTextAsync(
            session.MainSourcePath,
            "display: grid;",
            "display: flex;");
        await WaitForCssCompletionAsync(session, completionCount + 1);
        await WaitForStaticAssetCompletionAsync(
            session,
            staticAssetUpdateCount + 1,
            "the no-runtime component stylesheet swap to reach the browser");
        await RequireStylesheetAddressChangedAsync(
            page,
            ".viu.css",
            componentStylesheetAddress);
        await RequireComputedDisplayAsync(page, "hot-shell", "flex");
        byte[] changedComponentBundle = await File.ReadAllBytesAsync(
            session.ComponentBundlePath);
        Require(
            !originalComponentBundle.SequenceEqual(changedComponentBundle),
            "The no-runtime component style edit did not rewrite the generated bundle.");
        await RequireDocumentTokenAsync(page, documentToken);

        await page
            .Locator("[data-testid='utility-style-probe']")
            .EvaluateAsync<bool>(
                "element => { element.classList.add('opacity-50'); return true; }");
        await RequireComputedStylePropertyAsync(
            page,
            "utility-style-probe",
            "opacity",
            "1");
        byte[] originalUtilityBundle = await File.ReadAllBytesAsync(
            session.UtilityBundlePath);
        Require(
            !Encoding.UTF8.GetString(originalUtilityBundle).Contains(
                "opacity: 0.5;",
                StringComparison.Ordinal),
            "The utility bundle already contained the no-runtime scenario's new class.");
        string utilityStylesheetAddress = await ReadStylesheetAddressAsync(
            page,
            ".utilities.css");
        HotReloadProcessSnapshot utilitySnapshot = CaptureHotReloadProcessSnapshot(session);
        staticAssetUpdateCount = session.CountOutputLinesContaining(
            StaticAssetHotReloadCompletionMessage);
        completionCount = ReadCssCompletionCount(session.CssEventLogPath);

        await ReplaceSourceTextAsync(
            session.UtilityCandidateSourcePath,
            "class=\"hidden\"",
            "class=\"hidden opacity-50\"");
        await WaitForCssCompletionAsync(session, completionCount + 1);
        await WaitForStaticAssetCompletionAsync(
            session,
            staticAssetUpdateCount + 1,
            "the no-runtime utility stylesheet regeneration to reach the browser");
        await RequireStylesheetAddressChangedAsync(
            page,
            ".utilities.css",
            utilityStylesheetAddress);
        await RequireComputedStylePropertyAsync(
            page,
            "utility-style-probe",
            "opacity",
            "0.5");
        byte[] changedUtilityBundle = await File.ReadAllBytesAsync(
            session.UtilityBundlePath);
        Require(
            !originalUtilityBundle.SequenceEqual(changedUtilityBundle),
            "The no-runtime utility class edit did not rewrite the generated bundle.");
        Require(
            Encoding.UTF8.GetString(changedUtilityBundle).Contains(
                "opacity: 0.5;",
                StringComparison.Ordinal),
            "The no-runtime utility bundle does not contain the new opacity declaration.");
        await RequireDocumentTokenAsync(page, documentToken);
        await Task.Delay(HotReloadNoOperationObservationWindow);
        RequireStaticAssetOnlyUpdate(
            session,
            utilitySnapshot,
            "The no-runtime utility class edit");
        session.RequireRunning();
    }

    private static async Task RunScriptSignatureRestartScenarioAsync(
        IPage page,
        HotReloadWatchSession session)
    {
        await RequireTextAsync(page, "hot-heading", "Hot reload template v2");
        await RequireTextAsync(page, "hot-count", "2");

        string initialPageAddress = page.Url;
        string documentToken = await SetDocumentTokenAsync(page);
        await RequireDocumentTokenAsync(page, documentToken);
        HotReloadProcessSnapshot snapshot = CaptureHotReloadProcessSnapshot(session);
        int exactApplicationStartCount = session.CountOutputLinesContaining(
            $"App url: {session.Address.AbsoluteUri}");

        await ReplaceSourceTextAsync(
            session.MainSourcePath,
            "private void Increment() => CountReference.Value += 2;",
            "protected virtual void Increment() => CountReference.Value += 3;");

        await WaitUntilAsync(
            () => Task.FromResult(
                CountEditAndContinueDiagnostics(session)
                    > snapshot.EditAndContinueDiagnosticCount
                && session.CountOutputLinesContaining(
                    "Restart is needed to apply the changes.") > snapshot.RestartCount
                && session.CountOutputLinesContaining(
                    "App url: http://") > snapshot.ApplicationStartCount
                && session.CountOutputLinesContaining(
                    $"App url: {session.Address.AbsoluteUri}")
                    > exactApplicationStartCount
                && session.CountOutputLinesContaining(
                    "Now listening on:") > snapshot.ReadinessCount
                && session.CountOutputLinesContaining(
                    "Reloading browser.") > snapshot.BrowserReloadCount),
            "the script-signature edit to rebuild and restart on the pinned application address",
            HotReloadAssertionTimeout);
        await RequireTextAsync(
            page,
            "hot-heading",
            "Hot reload template v2",
            HotReloadAssertionTimeout);
        await RequireTextAsync(page, "hot-count", "0", HotReloadAssertionTimeout);

        int totalApplicationStarts = session.CountOutputLinesContaining(
            "App url: http://");
        int startsAtPinnedAddress = session.CountOutputLinesContaining(
            $"App url: {session.Address.AbsoluteUri}");
        Require(
            totalApplicationStarts == startsAtPinnedAddress,
            "The script-signature restart changed the application URL or port.");
        Require(
            session.CountOutputLinesContaining(ManagedHotReloadCompletionMessage)
                == snapshot.ManagedCompletionCount,
            "The rejected script-signature edit unexpectedly completed as a managed delta.");
        Require(
            string.Equals(
                page.Url,
                initialPageAddress,
                StringComparison.Ordinal)
            && string.Equals(
                page.Url,
                session.Address.AbsoluteUri,
                StringComparison.Ordinal),
            "The connected browser did not remain on the pinned application origin.");
        await RequireDocumentReloadAsync(page);
        await page.Locator("[data-testid='hot-increment']").ClickAsync();
        await RequireTextAsync(page, "hot-count", "3", HotReloadAssertionTimeout);
        await RequireTextAsync(
            page,
            "hot-conditional",
            "Conditional element landed",
            HotReloadAssertionTimeout);
        session.RequireRunning();
    }

    private static async Task RunUtilityCssNewClassScenarioAsync(
        IPage page,
        HotReloadWatchSession session)
    {
        await RequireTextAsync(page, "hot-heading", "Hot reload template v2");
        await RequireStylesheetRulesAsync(
            page,
            ".utilities.css",
            minimumRuleCount: 1);
        await RequireClassTokenAsync(page, "utility-probe", "hidden");
        await RequireComputedDisplayAsync(page, "utility-probe", "none");
        await RequireComputedStylePropertyAsync(
            page,
            "utility-style-probe",
            "opacity",
            "1");

        string documentToken = await SetDocumentTokenAsync(page);
        await RequireDocumentTokenAsync(page, documentToken);
        HotReloadProcessSnapshot snapshot = CaptureHotReloadProcessSnapshot(session);
        byte[] originalBundleContent = await File.ReadAllBytesAsync(
            session.UtilityBundlePath);
        Require(
            !Encoding.UTF8.GetString(originalBundleContent).Contains(
                "opacity: 0.5;",
                StringComparison.Ordinal),
            "The utility bundle already contained the new opacity utility before the edit.");
        string stylesheetAddress = await ReadStylesheetAddressAsync(
            page,
            ".utilities.css");
        Require(
            !string.IsNullOrEmpty(stylesheetAddress),
            "The initial utility stylesheet link has no address.");
        int staticAssetUpdateCount = session.CountOutputLinesContaining(
            StaticAssetHotReloadCompletionMessage);
        int completionCount = ReadCssCompletionCount(session.CssEventLogPath);

        await ReplaceSourceTextAsync(
            session.MainSourcePath,
            "<span data-testid=\"utility-style-probe\">",
            "<span class=\"opacity-50\" data-testid=\"utility-style-probe\">");
        await WaitForCssCompletionAsync(session, completionCount + 1);
        await RequireAcceptedManagedDeltaAsync(
            session,
            snapshot,
            "the new utility class to apply as a managed template delta");
        await WaitForStaticAssetCompletionAsync(
            session,
            staticAssetUpdateCount + 1,
            "the regenerated utility bundle to reach the browser");
        await RequireStylesheetAddressChangedAsync(
            page,
            ".utilities.css",
            stylesheetAddress);
        await RequireComputedStylePropertyAsync(
            page,
            "utility-style-probe",
            "opacity",
            "0.5");
        await RequireComputedDisplayAsync(page, "utility-probe", "none");

        byte[] changedBundleContent = await File.ReadAllBytesAsync(
            session.UtilityBundlePath);
        Require(
            !originalBundleContent.SequenceEqual(changedBundleContent),
            "Adding a new utility class did not rewrite the generated bundle.");
        Require(
            Encoding.UTF8.GetString(changedBundleContent).Contains(
                "opacity: 0.5;",
                StringComparison.Ordinal),
            "The regenerated utility bundle does not contain the new opacity declaration.");
        await RequireDocumentTokenAsync(page, documentToken);
        session.RequireRunning();
    }

    private static async Task RunUtilityCssLastSourceRemovalScenarioAsync(
        IPage page,
        HotReloadWatchSession session)
    {
        await RequireComputedStylePropertyAsync(
            page,
            "utility-style-probe",
            "opacity",
            "0.5");
        string documentToken = await SetDocumentTokenAsync(page);
        await RequireDocumentTokenAsync(page, documentToken);

        HotReloadProcessSnapshot templateSnapshot = CaptureHotReloadProcessSnapshot(session);
        int staticAssetUpdateCount = session.CountOutputLinesContaining(
            StaticAssetHotReloadCompletionMessage);
        int completionCount = ReadCssCompletionCount(session.CssEventLogPath);
        await ReplaceSourceTextAsync(
            session.MainSourcePath,
            "<span class=\"opacity-50\" data-testid=\"utility-style-probe\">",
            "<span data-testid=\"utility-style-probe\">");
        await WaitForCssCompletionAsync(session, completionCount + 1);
        await RequireAcceptedManagedDeltaAsync(
            session,
            templateSnapshot,
            "the utility-class removal to apply as a managed template delta");
        await WaitForStaticAssetCompletionAsync(
            session,
            staticAssetUpdateCount + 1,
            "the utility-class removal to reach the browser");
        await RequireComputedStylePropertyAsync(
            page,
            "utility-style-probe",
            "opacity",
            "1");
        await RequireComputedDisplayAsync(page, "utility-probe", "none");
        await RequireStylesheetRulesAsync(
            page,
            ".utilities.css",
            minimumRuleCount: 1);
        await RequireDocumentTokenAsync(page, documentToken);

        await page.Locator("[data-testid='hot-increment']").ClickAsync();
        await RequireTextAsync(page, "hot-count", "3");
        HotReloadProcessSnapshot removalSnapshot = CaptureHotReloadProcessSnapshot(session);
        string stylesheetAddress = await ReadStylesheetAddressAsync(
            page,
            ".utilities.css");
        staticAssetUpdateCount = session.CountOutputLinesContaining(
            StaticAssetHotReloadCompletionMessage);
        completionCount = ReadCssCompletionCount(session.CssEventLogPath);

        File.Delete(session.UtilityCandidateSourcePath);
        await WaitForCssCompletionAsync(session, completionCount + 1);
        await WaitUntilAsync(
            () => Task.FromResult(
                File.Exists(session.UtilityBundlePath)
                && new FileInfo(session.UtilityBundlePath).Length == 0),
            "the last utility source removal to preserve an empty hot-reload transport asset",
            HotReloadAssertionTimeout);
        await WaitForStaticAssetCompletionAsync(
            session,
            staticAssetUpdateCount + 1,
            "the retired utility bundle to reach the browser");
        await RequireStylesheetAddressChangedAsync(
            page,
            ".utilities.css",
            stylesheetAddress);
        await RequireStylesheetRulesAsync(
            page,
            ".utilities.css",
            minimumRuleCount: 0,
            exactRuleCount: 0);
        await RequireClassTokenAsync(page, "utility-probe", "hidden");
        await RequireComputedDisplayAsync(page, "utility-probe", "block");
        await Task.Delay(HotReloadNoOperationObservationWindow);
        RequireStaticAssetOnlyUpdate(
            session,
            removalSnapshot,
            "The last utility source removal");
        await RequireTextAsync(page, "hot-count", "3");
        await RequireDocumentTokenAsync(page, documentToken);
        session.RequireRunning();
    }

    private static async Task NavigateHotReloadAsync(
        IPage page,
        HotReloadWatchSession session)
    {
        int connectionCount = session.CountOutputLinesContaining(
            "Connected to refresh server.");
        await NavigateAsync(page, session.Address.AbsoluteUri);
        await WaitUntilAsync(
            () => Task.FromResult(
                session.CountOutputLinesContaining(
                    "Connected to refresh server.") > connectionCount),
            "the Playwright page to connect to the browser-refresh server",
            HotReloadAssertionTimeout);
    }

    private static async Task<string> PrepareRemountProbeAsync(IPage page)
    {
        await RequireTextAsync(page, "hot-count", "0");
        string documentToken = await SetDocumentTokenAsync(page);
        await RequireDocumentTokenAsync(page, documentToken);
        await page.Locator("[data-testid='hot-increment']").ClickAsync();
        await RequireTextAsync(page, "hot-count", "1");
        return documentToken;
    }

    private static async Task<string> SetDocumentTokenAsync(IPage page)
        => await page.EvaluateAsync<string>(
            "() => { const token = `${Date.now()}-${Math.random()}`; "
            + "globalThis.__viuHotReloadDocumentToken = token; return token; }");

    private static HotReloadProcessSnapshot CaptureHotReloadProcessSnapshot(
        HotReloadWatchSession session) => new(
            session.CountOutputLinesContaining(ManagedHotReloadCompletionMessage),
            CountEditAndContinueDiagnostics(session),
            session.CountOutputLinesContaining("Restart is needed to apply the changes."),
            session.CountOutputLinesContaining("App url: http://"),
            session.CountOutputLinesContaining("Now listening on:"),
            session.CountOutputLinesContaining("Reloading browser."));

    private static async Task RequireAcceptedManagedDeltaAsync(
        HotReloadWatchSession session,
        HotReloadProcessSnapshot snapshot,
        string description)
    {
        await WaitUntilAsync(
            () => Task.FromResult(
                session.CountOutputLinesContaining(ManagedHotReloadCompletionMessage)
                    > snapshot.ManagedCompletionCount),
            description,
            HotReloadAssertionTimeout);
        await Task.Delay(HotReloadNoOperationObservationWindow);
        Require(
            CountEditAndContinueDiagnostics(session)
                == snapshot.EditAndContinueDiagnosticCount,
            "An accepted managed update produced an Edit and Continue diagnostic.");
        Require(
            session.CountOutputLinesContaining(
                "Restart is needed to apply the changes.") == snapshot.RestartCount,
            "An accepted managed update requested an application restart.");
        Require(
            session.CountOutputLinesContaining("App url: http://")
                == snapshot.ApplicationStartCount,
            "An accepted managed update restarted the application host.");
        Require(
            session.CountOutputLinesContaining("Now listening on:")
                == snapshot.ReadinessCount,
            "An accepted managed update restarted the packaged Browser run host.");
        Require(
            session.CountOutputLinesContaining("Reloading browser.")
                == snapshot.BrowserReloadCount,
            "An accepted managed update reloaded the browser document.");
        session.RequireRunning();
    }

    private static void RequireStaticAssetOnlyUpdate(
        HotReloadWatchSession session,
        HotReloadProcessSnapshot snapshot,
        string description)
    {
        Require(
            session.CountOutputLinesContaining(ManagedHotReloadCompletionMessage)
                == snapshot.ManagedCompletionCount,
            $"{description} triggered a managed hot-reload update.");
        Require(
            CountEditAndContinueDiagnostics(session)
                == snapshot.EditAndContinueDiagnosticCount,
            $"{description} produced an Edit and Continue diagnostic.");
        Require(
            session.CountOutputLinesContaining(
                "Restart is needed to apply the changes.") == snapshot.RestartCount,
            $"{description} requested an application restart.");
        Require(
            session.CountOutputLinesContaining("App url: http://")
                == snapshot.ApplicationStartCount,
            $"{description} restarted the application host.");
        Require(
            session.CountOutputLinesContaining("Now listening on:")
                == snapshot.ReadinessCount,
            $"{description} restarted the packaged Browser run host.");
        Require(
            session.CountOutputLinesContaining("Reloading browser.")
                == snapshot.BrowserReloadCount,
            $"{description} reloaded the browser document.");
    }

    private static int CountEditAndContinueDiagnostics(HotReloadWatchSession session) =>
        session.CountOutputLinesContaining("warning ENC")
        + session.CountOutputLinesContaining("error ENC");

    private static async Task ReplaceSourceTextAsync(
        string path,
        string oldText,
        string newText)
    {
        string source = await File.ReadAllTextAsync(path);
        int firstIndex = source.IndexOf(oldText, StringComparison.Ordinal);
        if (firstIndex < 0
            || source.IndexOf(
                oldText,
                firstIndex + oldText.Length,
                StringComparison.Ordinal) >= 0)
        {
            throw new InvalidOperationException(
                $"Expected exactly one '{oldText}' occurrence in staged source {path}.");
        }

        await File.WriteAllTextAsync(
            path,
            source[..firstIndex] + newText + source[(firstIndex + oldText.Length)..],
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static async Task EditVisualStudioGeneratedAssetSourceAsync(string path)
    {
        string source = await File.ReadAllTextAsync(path);
        source = ReplaceExactlyOnce(
            source,
            "<div>",
            "<div class=\"opacity-50\">",
            path);
        source = ReplaceExactlyOnce(
            source,
            "border-left-width: 1px;",
            "border-left-width: 7px;",
            path);
        await File.WriteAllTextAsync(
            path,
            source,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string ReplaceExactlyOnce(
        string source,
        string oldText,
        string newText,
        string path)
    {
        int firstIndex = source.IndexOf(oldText, StringComparison.Ordinal);
        if (firstIndex < 0
            || source.IndexOf(
                oldText,
                firstIndex + oldText.Length,
                StringComparison.Ordinal) >= 0)
        {
            throw new InvalidOperationException(
                $"Expected exactly one '{oldText}' occurrence in staged source {path}.");
        }

        return source[..firstIndex] + newText + source[(firstIndex + oldText.Length)..];
    }

    private static async Task RewriteSourceWithoutContentChangeAsync(string path)
    {
        string source = await File.ReadAllTextAsync(path);
        await File.WriteAllTextAsync(
            path,
            source,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static async Task WaitForCssCompletionAsync(
        HotReloadWatchSession session,
        int expectedCount)
    {
        await WaitUntilAsync(
            () =>
            {
                session.RequireRunning();
                return Task.FromResult(
                    ReadCssCompletionCount(session.CssEventLogPath) >= expectedCount);
            },
            $"CSS regeneration completion {expectedCount}",
            HotReloadAssertionTimeout);
    }

    private static async Task WaitForCssCompletionAsync(
        VisualStudioHotReloadSession session,
        int expectedCount)
    {
        await WaitUntilAsync(
            () =>
            {
                session.RequireRunning();
                return Task.FromResult(
                    ReadCssCompletionCount(session.CssEventLogPath) >= expectedCount);
            },
            $"Visual Studio-shaped CSS regeneration completion {expectedCount}",
            HotReloadAssertionTimeout);
    }

    private static async Task<int> CountStaticFileUpdatesAsync(
        IPage page,
        string expectedPath)
    {
        return await page.EvaluateAsync<int>(
            "path => globalThis.__viuBrowserRefreshProbe.messages.reduce((count, message) => { "
            + "try { const payload = JSON.parse(message); "
            + "return count + (payload.type === 'UpdateStaticFile' && payload.path === path ? 1 : 0); } "
            + "catch { return count; } }, 0)",
            expectedPath);
    }

    private static async Task WaitForStaticFileUpdateAsync(
        IPage page,
        string expectedPath,
        int expectedCount)
    {
        await WaitUntilAsync(
            async () => await CountStaticFileUpdatesAsync(page, expectedPath) >= expectedCount,
            $"BrowserRefresh UpdateStaticFile for '{expectedPath}'",
            HotReloadAssertionTimeout);
    }

    private static async Task WaitForStaticAssetCompletionAsync(
        HotReloadWatchSession session,
        int expectedCount,
        string description)
    {
        await WaitUntilAsync(
            () =>
            {
                session.RequireRunning();
                return Task.FromResult(
                    session.CountOutputLinesContaining(
                        StaticAssetHotReloadCompletionMessage) >= expectedCount);
            },
            description,
            HotReloadAssertionTimeout);
    }

    private static int ReadCssCompletionCount(string path)
    {
        try
        {
            return File.Exists(path)
                ? File.ReadLines(path).Count(line => string.Equals(
                    line,
                    "complete:0",
                    StringComparison.Ordinal))
                : 0;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    private static async Task RequireComputedDisplayAsync(
        IPage page,
        string testIdentifier,
        string expected)
    {
        await WaitUntilAsync(
            async () => string.Equals(
                await page
                    .Locator($"[data-testid='{testIdentifier}']")
                    .EvaluateAsync<string>("element => getComputedStyle(element).display"),
                expected,
                StringComparison.Ordinal),
            $"'{testIdentifier}' display to become '{expected}'",
            HotReloadAssertionTimeout);
    }

    private static async Task RequireClassTokenAsync(
        IPage page,
        string testIdentifier,
        string expectedToken)
    {
        await WaitUntilAsync(
            async () => await page
                .Locator($"[data-testid='{testIdentifier}']")
                .EvaluateAsync<bool>(
                    "(element, token) => element.classList.contains(token)",
                    expectedToken),
            $"'{testIdentifier}' to retain the '{expectedToken}' class token",
            HotReloadAssertionTimeout);
    }

    private static async Task RequireComputedStylePropertyAsync(
        IPage page,
        string testIdentifier,
        string propertyName,
        string expected)
    {
        await WaitUntilAsync(
            async () => string.Equals(
                await page
                    .Locator($"[data-testid='{testIdentifier}']")
                    .EvaluateAsync<string>(
                        "(element, name) => getComputedStyle(element)"
                        + ".getPropertyValue(name).trim()",
                        propertyName),
                expected,
                StringComparison.Ordinal),
            $"'{testIdentifier}' computed {propertyName} to become '{expected}'",
            HotReloadAssertionTimeout);
    }

    private static async Task RequireStylesheetRulesAsync(
        IPage page,
        string suffix,
        int minimumRuleCount,
        int? exactRuleCount = null)
    {
        await WaitUntilAsync(
            async () =>
            {
                int count = await page.EvaluateAsync<int>(
                    "suffix => { const link = [...document.querySelectorAll('link[rel=stylesheet]')]"
                    + ".find(candidate => candidate.href.includes(suffix)); "
                    + "if (!link || !link.sheet) return -1; "
                    + "try { return link.sheet.cssRules.length; } catch { return -1; } }",
                    suffix);
                return exactRuleCount is int exact
                    ? count == exact
                    : count >= minimumRuleCount;
            },
            $"the {suffix} stylesheet to expose the expected rules",
            HotReloadAssertionTimeout);
    }

    private static async Task<string> ReadStylesheetAddressAsync(
        IPage page,
        string suffix)
    {
        return await page.EvaluateAsync<string>(
            "suffix => [...document.querySelectorAll('link[rel=stylesheet]')]"
            + ".find(candidate => candidate.href.includes(suffix))?.href ?? ''",
            suffix);
    }

    private static async Task RequireStylesheetAddressChangedAsync(
        IPage page,
        string suffix,
        string previousAddress)
    {
        await WaitUntilAsync(
            async () =>
            {
                string currentAddress = await ReadStylesheetAddressAsync(page, suffix);
                return !string.IsNullOrEmpty(currentAddress)
                    && !string.Equals(
                        currentAddress,
                        previousAddress,
                        StringComparison.Ordinal);
            },
            $"the {suffix} stylesheet link to be replaced",
            HotReloadAssertionTimeout);
    }

    private static async Task RequireDocumentTokenAsync(
        IPage page,
        string expected)
    {
        string? actual = await page.EvaluateAsync<string?>(
            "() => globalThis.__viuHotReloadDocumentToken ?? null");
        Require(
            string.Equals(actual, expected, StringComparison.Ordinal),
            "The browser document reloaded during an accepted hot update.");
    }

    private static async Task RequireDocumentReloadAsync(IPage page)
    {
        await WaitUntilAsync(
            async () =>
            {
                string? actual = await page.EvaluateAsync<string?>(
                    "() => globalThis.__viuHotReloadDocumentToken ?? null");
                return actual is null;
            },
            "the readiness-triggered browser Reload to replace the document",
            HotReloadAssertionTimeout);
    }

    private async Task RunBrowserEngineAsync(
        IPlaywright playwright,
        BrowserEngine browserEngine,
        Uri browserAddress,
        Uri hydrationAddress)
    {
        IBrowserType browserType = browserEngine switch
        {
            BrowserEngine.Chromium => playwright.Chromium,
            BrowserEngine.Firefox => playwright.Firefox,
            BrowserEngine.WebKit => playwright.Webkit,
            _ => throw new ArgumentOutOfRangeException(nameof(browserEngine)),
        };
        Console.WriteLine($"Launching {browserEngine}...");
        await using IBrowser browser = await browserType.LaunchAsync(
            new BrowserTypeLaunchOptions
            {
                Headless = !_options.Headed,
            });

        await RunScenarioAsync(
            browser,
            browserEngine,
            "clean-boot",
            async page =>
            {
                await NavigateAsync(page, new Uri(browserAddress, "#/").AbsoluteUri);
                await RequireTextAsync(page, "route-heading", "Home route");
            });
        await RunScenarioAsync(
            browser,
            browserEngine,
            "route-reactivity-model-list-cleanup-scheduler",
            page => RunInteractiveScenarioAsync(page, browserAddress));
        await RunScenarioAsync(
            browser,
            browserEngine,
            "server-adaptor-hydration-visible",
            page => RunHydrationScenarioAsync(page, hydrationAddress));

        if (_options.MeasureStartup && browserEngine == BrowserEngine.Chromium)
        {
            await MeasureStartupAsync(browser, browserAddress);
        }
    }

    private async Task RunScenarioAsync(
        IBrowser browser,
        BrowserEngine browserEngine,
        string scenario,
        Func<IPage, Task> execute)
    {
        string engineDirectory = Path.Combine(
            _options.ArtifactDirectory,
            browserEngine.ToString().ToLowerInvariant());
        Directory.CreateDirectory(engineDirectory);
        string screenshotPath = Path.Combine(engineDirectory, scenario + ".png");
        string tracePath = Path.Combine(engineDirectory, scenario + ".trace.zip");
        List<string> browserErrors = [];
        List<string> requestFailures = [];
        Stopwatch stopwatch = Stopwatch.StartNew();

        await using IBrowserContext context = await browser.NewContextAsync(
            new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize
                {
                    Width = 1280,
                    Height = 720,
                },
            });
        await context.Tracing.StartAsync(
            new TracingStartOptions
            {
                Screenshots = true,
                Snapshots = true,
                Sources = true,
            });
        IPage page = await context.NewPageAsync();
        page.Console += (_, message) =>
        {
            if (string.Equals(message.Type, "error", StringComparison.OrdinalIgnoreCase))
            {
                lock (browserErrors)
                {
                    browserErrors.Add($"console: {message.Text}");
                }
            }
        };
        page.PageError += (_, message) =>
        {
            lock (browserErrors)
            {
                browserErrors.Add($"page: {message}");
            }
        };
        page.RequestFailed += (_, request) =>
        {
            if (_options.HotReloadProjectPath is not null
                && IsExpectedHotReloadRequestAbort(request))
            {
                return;
            }

            lock (requestFailures)
            {
                requestFailures.Add(
                    $"{request.Method} {request.Url}: {request.Failure}");
            }
        };

        try
        {
            await execute(page);
            await Task.Delay(100);
            string[] errors;
            lock (browserErrors)
            {
                errors = browserErrors.ToArray();
            }

            if (errors.Length > 0)
            {
                throw new InvalidOperationException(
                    "Unexpected browser errors:\n" + string.Join("\n", errors));
            }

            string[] failedRequests;
            lock (requestFailures)
            {
                failedRequests = requestFailures.ToArray();
            }

            if (failedRequests.Length > 0)
            {
                throw new InvalidOperationException(
                    "Browser requests failed:\n" + string.Join("\n", failedRequests));
            }

            await context.Tracing.StopAsync();
            stopwatch.Stop();
            _results.Add(
                new ScenarioResult(
                    browserEngine.ToString(),
                    scenario,
                    true,
                    stopwatch.Elapsed.TotalMilliseconds,
                    null,
                    null,
                    null));
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            try
            {
                await page.ScreenshotAsync(
                    new PageScreenshotOptions
                    {
                        Path = screenshotPath,
                        FullPage = true,
                    });
            }
            catch (Exception screenshotException)
            {
                File.WriteAllText(
                    screenshotPath + ".error.txt",
                    screenshotException.ToString());
            }

            try
            {
                await context.Tracing.StopAsync(
                    new TracingStopOptions { Path = tracePath });
            }
            catch (Exception traceException)
            {
                File.WriteAllText(tracePath + ".error.txt", traceException.ToString());
            }

            _results.Add(
                new ScenarioResult(
                    browserEngine.ToString(),
                    scenario,
                    false,
                    stopwatch.Elapsed.TotalMilliseconds,
                    exception.ToString(),
                    screenshotPath,
                    tracePath));
        }
    }

    private async Task RunConnectedHotReloadScenarioAsync(
        IBrowserContext context,
        IPage page,
        BrowserEngine browserEngine,
        string scenario,
        List<string> browserErrors,
        List<string> requestFailures,
        Func<IPage, Task> execute)
    {
        string engineDirectory = Path.Combine(
            _options.ArtifactDirectory,
            browserEngine.ToString().ToLowerInvariant());
        Directory.CreateDirectory(engineDirectory);
        string screenshotPath = Path.Combine(engineDirectory, scenario + ".png");
        string tracePath = Path.Combine(engineDirectory, scenario + ".trace.zip");
        int browserErrorCount;
        lock (browserErrors)
        {
            browserErrorCount = browserErrors.Count;
        }

        int requestFailureCount;
        lock (requestFailures)
        {
            requestFailureCount = requestFailures.Count;
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        bool tracingStarted = false;
        try
        {
            await context.Tracing.StartAsync(
                new TracingStartOptions
                {
                    Screenshots = true,
                    Snapshots = true,
                    Sources = true,
                });
            tracingStarted = true;
            await execute(page);
            await Task.Delay(100);

            string[] errors;
            lock (browserErrors)
            {
                errors = browserErrors.Skip(browserErrorCount).ToArray();
            }

            if (errors.Length > 0)
            {
                throw new InvalidOperationException(
                    "Unexpected browser errors:\n" + string.Join("\n", errors));
            }

            string[] failedRequests;
            lock (requestFailures)
            {
                failedRequests = requestFailures.Skip(requestFailureCount).ToArray();
            }

            if (failedRequests.Length > 0)
            {
                throw new InvalidOperationException(
                    "Browser requests failed:\n" + string.Join("\n", failedRequests));
            }

            await context.Tracing.StopAsync();
            tracingStarted = false;
            stopwatch.Stop();
            _results.Add(
                new ScenarioResult(
                    browserEngine.ToString(),
                    scenario,
                    true,
                    stopwatch.Elapsed.TotalMilliseconds,
                    null,
                    null,
                    null));
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            try
            {
                await page.ScreenshotAsync(
                    new PageScreenshotOptions
                    {
                        Path = screenshotPath,
                        FullPage = true,
                    });
            }
            catch (Exception screenshotException)
            {
                File.WriteAllText(
                    screenshotPath + ".error.txt",
                    screenshotException.ToString());
            }

            if (tracingStarted)
            {
                try
                {
                    await context.Tracing.StopAsync(
                        new TracingStopOptions { Path = tracePath });
                }
                catch (Exception traceException)
                {
                    File.WriteAllText(tracePath + ".error.txt", traceException.ToString());
                }
            }

            _results.Add(
                new ScenarioResult(
                    browserEngine.ToString(),
                    scenario,
                    false,
                    stopwatch.Elapsed.TotalMilliseconds,
                    exception.ToString(),
                    screenshotPath,
                    tracePath));
        }
    }

    private static bool IsExpectedHotReloadRequestAbort(IRequest request)
    {
        if (!string.Equals(
                request.Failure,
                "net::ERR_ABORTED",
                StringComparison.Ordinal))
        {
            return false;
        }

        return request.Url.Contains(
                "/_framework/blazor-hotreload",
                StringComparison.Ordinal)
            || request.Url.Contains(
                "/_framework/clear-browser-cache",
                StringComparison.Ordinal);
    }

    private static async Task RunInteractiveScenarioAsync(IPage page, Uri browserAddress)
    {
        await NavigateAsync(page, new Uri(browserAddress, "#/").AbsoluteUri);
        await RequireTextAsync(page, "route-heading", "Home route");

        // WHATWG HTML defines history.scrollRestoration and CSSOM View defines the offsets in Viu's
        // saved-position ledger. This exercises the shipped module in an isolated same-origin realm:
        // https://html.spec.whatwg.org/multipage/nav-history-apis.html#dom-history-scroll-restoration-dev
        // https://drafts.csswg.org/cssom-view/#dom-window-scrollx
        string activeScrollRestoration = await page.EvaluateAsync<string>(
            "window.history.scrollRestoration");
        Require(
            string.Equals(activeScrollRestoration, "manual", StringComparison.Ordinal),
            "Browser.Router did not suspend native scroll restoration while its history was active.");
        bool[] restorationLifecycle = await page.EvaluateAsync<bool[]>(
            """
            async () => {
                const frame = document.createElement('iframe')
                frame.src = 'about:blank'
                document.body.appendChild(frame)
                try {
                    const frameWindow = frame.contentWindow
                    const previous = frameWindow.history.scrollRestoration
                    const moduleAddress = new URL(
                        '/_content/Assimalign.Viu.Browser.Router/viu-history.js',
                        window.location.href)
                    moduleAddress.searchParams.set('contract', `${Date.now()}-${Math.random()}`)
                    const module = await frameWindow.eval(
                        `import(${JSON.stringify(moduleAddress.href)})`)
                    module.history.subscribe(1000001)
                    const firstIsManual = frameWindow.history.scrollRestoration === 'manual'
                    module.history.subscribe(1000002)
                    module.history.unsubscribe(1000001)
                    const coexistingIsManual = frameWindow.history.scrollRestoration === 'manual'
                    module.history.unsubscribe(1000002)
                    const previousWasRestored =
                        frameWindow.history.scrollRestoration === previous
                    return [firstIsManual, coexistingIsManual, previousWasRestored]
                }
                finally {
                    frame.remove()
                }
            }
            """);
        Require(
            restorationLifecycle.SequenceEqual([true, true, true]),
            "The history module did not hold manual restoration until the last subscription disposed.");

        await page.EvaluateAsync("window.scrollTo({ left: 0, top: 900, behavior: 'instant' })");
        await RequireScrollOffsetAsync(page, 900);

        // Programmatic dispatch preserves the deliberate leaving offset; Locator.ClickAsync would
        // scroll the top-of-page link into view before the history module captures the ledger entry.
        await page.EvaluateAsync(
            "document.querySelector('[data-testid=route-second]').click()");
        await RequireTextAsync(page, "route-heading", "Second route");
        await RequireScrollOffsetAsync(page, 0);
        await page.EvaluateAsync("window.scrollTo({ left: 0, top: 500, behavior: 'instant' })");
        await RequireScrollOffsetAsync(page, 500);

        await page.GoBackAsync();
        await RequireTextAsync(page, "route-heading", "Home route");
        await RequireScrollOffsetAsync(page, 900);

        await page.GoForwardAsync();
        await RequireTextAsync(page, "route-heading", "Second route");
        await RequireScrollOffsetAsync(page, 500);

        await page.Locator("[data-testid='route-home']").ClickAsync();
        await RequireTextAsync(page, "route-heading", "Home route");

        await page.Locator("[data-testid='increment']").ClickAsync();
        await RequireTextAsync(page, "count-value", "1");

        await page.Locator("[data-testid='model-input']").FillAsync("browser round trip");
        await RequireTextAsync(page, "model-output", "browser round trip");

        await page.EvaluateAsync(
            "globalThis.__viuKeyedNode = document.querySelector('[data-item=alpha]')");
        await page.Locator("[data-testid='reorder']").ClickAsync();
        await WaitUntilAsync(
            async () =>
            {
                IReadOnlyList<string> values = await page
                    .Locator("[data-testid='keyed-list'] li")
                    .AllTextContentsAsync();
                return values.SequenceEqual(["gamma", "beta", "alpha"]);
            },
            "the keyed list to reverse");
        bool preservedKeyedNode = await page.EvaluateAsync<bool>(
            "globalThis.__viuKeyedNode === document.querySelector('[data-item=alpha]')");
        Require(preservedKeyedNode, "The keyed move replaced the alpha DOM node.");

        await page.Locator("[data-testid='cleanup-probe']").ClickAsync();
        await RequireTextAsync(page, "cleanup-probe-count", "1");

        await page.EvaluateAsync(
            "globalThis.__viuUnmountedNode = document.querySelector('[data-testid=cleanup-probe]')");
        await page.Locator("[data-testid='toggle-child']").ClickAsync();
        await WaitUntilAsync(
            async () => await page.Locator("[data-testid='cleanup-probe']").CountAsync() == 0,
            "the child component DOM to be removed");
        bool orphanDisconnected = await page.EvaluateAsync<bool>(
            "globalThis.__viuUnmountedNode && !globalThis.__viuUnmountedNode.isConnected");
        Require(orphanDisconnected, "The unmounted child remained connected to the DOM.");
        await page.Locator("[data-testid='capture-diagnostics']").ClickAsync();
        await RequireTextAsync(page, "registry-capture-generation", "1");
        string firstRegistryBaseline = await ReadTextAsync(page, "registry-diagnostics");

        await page.Locator("[data-testid='toggle-child']").ClickAsync();
        await WaitUntilAsync(
            async () => await page.Locator("[data-testid='cleanup-probe']").CountAsync() == 1,
            "the child component DOM to remount");
        await page.Locator("[data-testid='toggle-child']").ClickAsync();
        await WaitUntilAsync(
            async () => await page.Locator("[data-testid='cleanup-probe']").CountAsync() == 0,
            "the remounted child component DOM to be removed");
        await page.Locator("[data-testid='capture-diagnostics']").ClickAsync();
        await RequireTextAsync(page, "registry-capture-generation", "2");
        string secondRegistryBaseline = await ReadTextAsync(page, "registry-diagnostics");
        Require(
            string.Equals(
                firstRegistryBaseline,
                secondRegistryBaseline,
                StringComparison.Ordinal),
            $"Browser registries leaked across remount/unmount: "
            + $"first={firstRegistryBaseline}, second={secondRegistryBaseline}.");

        await page.Locator("[data-testid='next-tick']").ClickAsync();
        await RequireTextAsync(
            page,
            "next-tick-result",
            "rendered-before-next-tick");
    }

    private static Task RequireScrollOffsetAsync(IPage page, double expected)
    {
        return WaitUntilAsync(
            async () => Math.Abs(await page.EvaluateAsync<double>("window.scrollY") - expected) < 1,
            $"the router to restore scroll offset {expected.ToString(CultureInfo.InvariantCulture)}");
    }

    private static async Task RunHydrationScenarioAsync(IPage page, Uri hydrationAddress)
    {
        await NavigateAsync(page, hydrationAddress.AbsoluteUri);
        await RequireTextAsync(page, "hydrated-heading", "SSR hydrated route");
        string markupSource = await page.EvaluateAsync<string>(
            "globalThis.__viuServerMarkupSource");
        Require(
            string.Equals(markupSource, "ServerRenderAdaptor", StringComparison.Ordinal),
            "The hydration document was not produced through ServerRenderAdaptor.");
        bool headingWasAdopted = await page.EvaluateAsync<bool>(
            "globalThis.__viuServerHeading === document.querySelector('[data-testid=hydrated-heading]')");
        Require(headingWasAdopted, "Browser hydration replaced the server-rendered heading node.");

        ILocator lazyAction = page.Locator("[data-testid='lazy-action']");
        await RequireTextAsync(page, "lazy-action", "Lazy waiting: 0");
        LocatorBoundingBoxResult? initialBox = await lazyAction.BoundingBoxAsync();
        Require(
            initialBox is not null && initialBox.Y > 720,
            "The visible-hydration target must begin below the viewport.");
        await page.DispatchEventAsync("[data-testid='lazy-action']", "click");
        await Task.Delay(150);
        await RequireTextAsync(page, "lazy-action", "Lazy waiting: 0");

        await lazyAction.ScrollIntoViewIfNeededAsync();
        await RequireTextAsync(page, "lazy-action", "Lazy ready: 0");
        await lazyAction.ClickAsync();
        await RequireTextAsync(page, "lazy-action", "Lazy ready: 1");
    }

    private async Task MeasureStartupAsync(IBrowser browser, Uri browserAddress)
    {
        for (int index = 0; index < _options.StartupWarmupRuns; index++)
        {
            _ = await MeasureOneStartupAsync(browser, browserAddress);
        }

        double[] measurements = new double[_options.StartupMeasuredRuns];
        for (int index = 0; index < measurements.Length; index++)
        {
            measurements[index] = await MeasureOneStartupAsync(browser, browserAddress);
            Console.WriteLine(
                $"Boot-to-interactive measurement {index + 1}/{measurements.Length}: "
                + $"{measurements[index].ToString("F1", CultureInfo.InvariantCulture)} ms");
        }

        double[] ordered = measurements.Order().ToArray();
        double median = ordered.Length % 2 == 0
            ? (ordered[(ordered.Length / 2) - 1] + ordered[ordered.Length / 2]) / 2
            : ordered[ordered.Length / 2];
        StartupMeasurementResult result = new(
            1,
            DateTimeOffset.UtcNow,
            "EndToEndBrowserApp",
            "boot-to-interactive",
            BrowserEngine.Chromium.ToString(),
            _options.StartupWarmupRuns,
            _options.StartupMeasuredRuns,
            measurements,
            median);
        string resultPath = _options.StartupResultsPath!;
        string? directory = Path.GetDirectoryName(resultPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(
            resultPath,
            System.Text.Json.JsonSerializer.Serialize(
                result,
                HarnessJSONSerializerContext.Default.StartupMeasurementResult));
        Console.WriteLine(
            $"Boot-to-interactive median: {median.ToString("F1", CultureInfo.InvariantCulture)} ms -> {resultPath}");
    }

    private static async Task<double> MeasureOneStartupAsync(
        IBrowser browser,
        Uri browserAddress)
    {
        await using IBrowserContext context = await browser.NewContextAsync(
            new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1280, Height = 720 },
            });
        IPage page = await context.NewPageAsync();
        List<string> errors = [];
        page.Console += (_, message) =>
        {
            if (string.Equals(message.Type, "error", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(message.Text);
            }
        };
        page.PageError += (_, message) => errors.Add(message);
        Stopwatch stopwatch = Stopwatch.StartNew();
        await NavigateAsync(page, new Uri(browserAddress, "#/").AbsoluteUri);
        await RequireTextAsync(page, "route-heading", "Home route");
        await page.Locator("[data-testid='increment']").ClickAsync();
        await RequireTextAsync(page, "count-value", "1");
        stopwatch.Stop();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Startup emitted browser errors: " + string.Join(" | ", errors));
        }

        return stopwatch.Elapsed.TotalMilliseconds;
    }

    private async Task WriteResultSummaryAsync()
    {
        string resultPath = Path.Combine(_options.ArtifactDirectory, "results.json");
        await File.WriteAllTextAsync(
            resultPath,
            System.Text.Json.JsonSerializer.Serialize(
                new HarnessResultSummary(
                    1,
                    DateTimeOffset.UtcNow,
                    _results),
                HarnessJSONSerializerContext.Default.HarnessResultSummary));
    }

    private static async Task NavigateAsync(IPage page, string address)
    {
        IResponse? response = await page.GotoAsync(
            address,
            new PageGotoOptions
            {
                WaitUntil = WaitUntilState.Commit,
                Timeout = 120_000,
            });
        Require(
            response is not null && response.Ok,
            $"Navigation to {address} failed with status {response?.Status}.");
    }

    private static Task RequireTextAsync(
        IPage page,
        string testIdentifier,
        string expected,
        TimeSpan? timeout = null) => WaitUntilAsync(
        async () =>
        {
            ILocator locator = page.Locator($"[data-testid='{testIdentifier}']");
            if (await locator.CountAsync() != 1)
            {
                return false;
            }

            string? text = await locator.TextContentAsync();
            return string.Equals(text?.Trim(), expected, StringComparison.Ordinal);
        },
        $"'{testIdentifier}' to contain '{expected}'",
        timeout);

    private static Task RequireAbsentAsync(
        IPage page,
        string testIdentifier,
        TimeSpan? timeout = null) => WaitUntilAsync(
        async () => await page
            .Locator($"[data-testid='{testIdentifier}']")
            .CountAsync() == 0,
        $"'{testIdentifier}' to be absent",
        timeout);

    private static async Task<string> ReadTextAsync(
        IPage page,
        string testIdentifier)
    {
        ILocator locator = page.Locator($"[data-testid='{testIdentifier}']");
        await WaitUntilAsync(
            async () => await locator.CountAsync() == 1,
            $"'{testIdentifier}' to exist");
        return (await locator.TextContentAsync())?.Trim() ?? string.Empty;
    }

    private static async Task WaitUntilAsync(
        Func<Task<bool>> predicate,
        string description,
        TimeSpan? timeout = null)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        Exception? lastFailure = null;
        while (stopwatch.Elapsed < (timeout ?? AssertionTimeout))
        {
            try
            {
                if (await predicate())
                {
                    return;
                }
            }
            catch (PlaywrightException exception)
            {
                lastFailure = exception;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException(
            $"Timed out waiting for {description}.",
            lastFailure);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private readonly record struct HotReloadProcessSnapshot(
        int ManagedCompletionCount,
        int EditAndContinueDiagnosticCount,
        int RestartCount,
        int ApplicationStartCount,
        int ReadinessCount,
        int BrowserReloadCount);
}
