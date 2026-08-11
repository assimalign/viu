using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Playwright;

namespace Assimalign.Viu.Testing.EndToEnd;

internal sealed class EndToEndHarness
{
    private static readonly TimeSpan AssertionTimeout = TimeSpan.FromSeconds(30);
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
        await using StaticWebServer browserServer =
            StaticWebServer.Start(_options.BrowserRootDirectory);
        await using StaticWebServer hydrationServer =
            StaticWebServer.Start(_options.HydrationRootDirectory);
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

    private static async Task RunInteractiveScenarioAsync(IPage page, Uri browserAddress)
    {
        await NavigateAsync(page, new Uri(browserAddress, "#/").AbsoluteUri);
        await RequireTextAsync(page, "route-heading", "Home route");

        await page.Locator("[data-testid='route-second']").ClickAsync();
        await RequireTextAsync(page, "route-heading", "Second route");
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
        string expected) => WaitUntilAsync(
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
        $"'{testIdentifier}' to contain '{expected}'");

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
        string description)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        Exception? lastFailure = null;
        while (stopwatch.Elapsed < AssertionTimeout)
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
