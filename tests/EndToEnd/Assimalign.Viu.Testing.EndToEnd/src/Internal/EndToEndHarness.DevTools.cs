using System;
using System.Threading.Tasks;

using Microsoft.Playwright;

namespace Assimalign.Viu.Testing.EndToEnd;

internal sealed partial class EndToEndHarness
{
    private async Task RunDevToolsLaneAsync(string publishRoot)
    {
        await using StaticWebServer server = StaticWebServer.Start(publishRoot);
        Console.WriteLine($"DevTools fixture: {server.Address}");
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = !_options.Headed });
        await RunScenarioAsync(
            browser,
            BrowserEngine.Chromium,
            "packaged-devtools-inspection-client",
            page => RunDevToolsScenarioAsync(page, server.Address));
    }

    // [DVT-13], [DVT-14]: every panel action goes through the published Viu UI and real
    // postMessage transport. The scenario never calls a runtime/session object from JavaScript.
    private static async Task RunDevToolsScenarioAsync(IPage page, Uri address)
    {
        await NavigateAsync(page, address.AbsoluteUri);
        await RequireTextAsync(page, "inspected-count", "0");
        IFrameLocator panel = page.FrameLocator("[data-testid='devtools-frame']");
        ILocator root = panel.Locator(
            "[data-testid='devtools-tree-node'][data-component-name='InspectedCounter']");
        await WaitUntilAsync(
            async () => await root.CountAsync() == 1,
            "the protocol tree to show the inspected root",
            TimeSpan.FromSeconds(90));
        Require(
            await panel.Locator("[data-testid='devtools-tree-node']").CountAsync() == 1,
            "The inspected tree included the panel's independent component graph.");

        await page.GetByTestId("inspected-toggle-child").ClickAsync();
        ILocator child = panel.Locator(
            "[data-testid='devtools-tree-node'][data-component-name='InspectedChild']");
        await WaitUntilAsync(
            async () => await child.CountAsync() == 1,
            "a mounted child to appear incrementally in the tree");
        await page.GetByTestId("inspected-toggle-child").ClickAsync();
        await WaitUntilAsync(
            async () => await child.CountAsync() == 0,
            "the unmounted child to leave the tree");

        await root.ClickAsync();
        ILocator count = panel.Locator(
            "[data-testid='devtools-state-row'][data-section='state'][data-path='count']");
        await WaitUntilAsync(
            async () => await count.CountAsync() == 1,
            "the selected component's snapshot to render its count reference");
        Require(
            await panel.Locator("[data-testid='devtools-state-row'][data-path='count.value']").CountAsync() == 0,
            "A collapsed reference was eagerly expanded.");
        await count.GetByTestId("devtools-expand").ClickAsync();
        ILocator countValue = panel.Locator(
            "[data-testid='devtools-state-row'][data-section='state'][data-path='count.value']");
        await WaitUntilAsync(
            async () => await countValue.CountAsync() == 1
                && (await countValue.TextContentAsync())!.Contains("0", StringComparison.Ordinal),
            "lazy reference expansion to render its current value");
        await countValue.GetByTestId("devtools-edit-select").ClickAsync();
        await panel.GetByTestId("devtools-edit-value").FillAsync("41");
        await panel.GetByTestId("devtools-edit-apply").ClickAsync();
        await RequireTextAsync(page, "inspected-count", "41");
        await WaitUntilAsync(
            async () => (await panel.GetByTestId("devtools-edit-result").TextContentAsync())!
                .Contains("Applied", StringComparison.Ordinal),
            "the accepted edit response to reach the panel");

        ILocator writes = panel.Locator(
            "[data-testid='devtools-timeline-event'][data-kind='state.write']")
            .Filter(new LocatorFilterOptions { HasText = "count" });
        await WaitUntilAsync(
            async () =>
            {
                if (await writes.CountAsync() > 0)
                {
                    return true;
                }
                ILocator next = panel.GetByTestId("devtools-timeline-next");
                if (await next.IsEnabledAsync())
                {
                    await next.ClickAsync();
                }
                return false;
            },
            "the state-write timeline record to reach the panel");
        await writes.Last.ClickAsync();
        string? correlation = await writes.Last.GetAttributeAsync("data-correlation");
        Require(!string.IsNullOrEmpty(correlation) && correlation != "0",
            "A state write did not carry a scheduler correlation identifier.");
        await WaitUntilAsync(
            async () => await panel.Locator(
                "[data-testid='devtools-timeline-event'][data-related='true']").CountAsync() > 1,
            "selecting a timeline write to highlight its correlated chain");
        await WaitUntilAsync(
            async () => await panel.Locator(
                "[data-testid='devtools-timeline-event'][data-related='true'][data-kind='component.updated']")
                .CountAsync() > 0,
            "the correlated chain to include the inspected component update");

        await panel.Locator(
            "[data-testid='devtools-inspector-select'][data-inspector-identifier='sample-inventory']")
            .ClickAsync();
        ILocator warehouse = panel.GetByTestId("devtools-inspector-node")
            .Filter(new LocatorFilterOptions { HasText = "Sample warehouse" });
        await WaitUntilAsync(
            async () => await warehouse.CountAsync() == 1,
            "the generic inspector tree to render the sample provider");
        await warehouse.ClickAsync();
        await WaitUntilAsync(
            async () => (await panel.GetByTestId("devtools-inspector-state").TextContentAsync())!
                .Contains("Warehouse north", StringComparison.Ordinal),
            "the generic inspector state to render an unknown provider's fields");

        await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.Commit });
        await RequireTextAsync(page, "inspected-count", "0");
        await WaitUntilAsync(
            async () => await root.CountAsync() == 1,
            "the reloaded inspected app and panel to negotiate a fresh tree",
            TimeSpan.FromSeconds(90));
        Require(await child.CountAsync() == 0,
            "A previous runtime's unmounted child survived a new handshake.");
    }
}
