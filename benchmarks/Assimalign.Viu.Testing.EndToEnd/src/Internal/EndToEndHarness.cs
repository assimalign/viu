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
        await RunScenarioAsync(
            browser,
            BrowserEngine.Chromium,
            "packaged-vue-watch-css-and-remount",
            page => RunHotReloadScenarioAsync(page, session));
        await session.StopAsync();
    }

    private static async Task RunHotReloadScenarioAsync(
        IPage page,
        HotReloadWatchSession session)
    {
        await NavigateAsync(page, session.Address.AbsoluteUri);
        await RequireTextAsync(page, "hot-heading", "Hot reload template v1");
        await RequireTextAsync(page, "hot-count", "0");
        await RequireStylesheetRulesAsync(
            page,
            ".viu.css",
            minimumRuleCount: 1);
        await RequireComputedDisplayAsync(page, "hot-shell", "grid");

        string documentToken = await page.EvaluateAsync<string>(
            "() => { const token = `${Date.now()}-${Math.random()}`; "
            + "globalThis.__viuHotReloadDocumentToken = token; return token; }");
        await page.Locator("[data-testid='hot-increment']").ClickAsync();
        await RequireTextAsync(page, "hot-count", "1");

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
        await RequireTextAsync(page, "hot-count", "1");
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
        await RequireTextAsync(page, "hot-count", "1");
        await RequireDocumentTokenAsync(page, documentToken);

        await ReplaceSourceTextAsync(
            session.MainSourcePath,
            "Hot reload template v1",
            "Hot reload template v2");
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
        await ReplaceSourceTextAsync(
            session.MainSourcePath,
            "CountReference.Value++;",
            "CountReference.Value += 2;");
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
}
