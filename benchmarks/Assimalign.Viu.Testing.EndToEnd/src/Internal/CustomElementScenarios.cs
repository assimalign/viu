using System;
using System.Threading.Tasks;

using Microsoft.Playwright;

namespace Assimalign.Viu.Testing.EndToEnd;

/// <summary>Packaged browser conformance for [V01.01.04.08] and the [CEL-*] clauses.</summary>
internal static class CustomElementScenarios
{
    internal static async Task RunAsync(IPage page, Uri address)
    {
        bool invalidAttributeWarning = false;
        page.Console += (_, message) =>
        {
            if (message.Type == "warning" && message.Text.Contains("count", StringComparison.Ordinal))
            {
                invalidAttributeWarning = true;
            }
        };
        await page.GotoAsync(address.AbsoluteUri);
        await RequireStateAsync(page, "document.getElementById('status').textContent === 'ready'",
            "the vanilla HTML fixture to boot");
        await RequireStateAsync(page, """
            (() => {
                const counter = customElementFixture.counter;
                const root = counter.shadowRoot;
                return root?.querySelector('[data-testid=count]')?.textContent.trim() === '3'
                    && counter.count === 3 && counter.enabled === true && counter.stepSize === 1.5
                    && customElementFixture.managed.GetMountCount() === 2;
            })()
            """, "pre-existing elements to upgrade with typed attributes");
        await RequireStateAsync(page, """
            (() => {
                const root = customElementFixture.counter.shadowRoot;
                return root.querySelector('slot:not([name])')?.assignedElements()[0]?.id === 'default-child'
                    && root.querySelector('slot[name=label]')?.assignedElements()[0]?.id === 'named-child'
                    && getComputedStyle(root.querySelector('.custom-element-surface')).color === 'rgb(25, 80, 120)'
                    && root.querySelector('link[rel=stylesheet]') === null
                    && root.adoptedStyleSheets.some(sheet =>
                        Array.from(sheet.cssRules).some(rule => rule.cssText.includes('.custom-element-surface')))
                    && customElementFixture.lightCounter.shadowRoot === null
                    && customElementFixture.lightCounter.querySelector('slot') === null
                    && getComputedStyle(customElementFixture.lightCounter.querySelector('.custom-element-surface')).color
                        === 'rgb(25, 80, 120)';
            })()
            """, "native slots and bundled styles in shadow and light DOM");
        await page.EvaluateAsync("customElementFixture.defaultChild.remove()");
        await RequireStateAsync(page, """
            (() => {
                const container = customElementFixture.counter.shadowRoot
                    .querySelector('[data-testid=default-slot]');
                return container.textContent.trim() === 'Default fallback'
                    && container.querySelector('slot:not([name])') === null;
            })()
            """, "removing the last default slottable to restore authored fallback content");
        await page.EvaluateAsync(
            "customElementFixture.counter.prepend(customElementFixture.defaultChild)");
        await RequireStateAsync(page, """
            (() => {
                const slot = customElementFixture.counter.shadowRoot.querySelector('slot:not([name])');
                return slot?.assignedElements()[0]?.id === 'default-child';
            })()
            """, "adding a default slottable to restore the native default outlet");

        int updatesBeforeAttributes = await ReadUpdateCountAsync(page);
        await page.Locator("#change-attributes").ClickAsync();
        await RequireStateAsync(page, """
            (() => {
                const counter = customElementFixture.counter;
                return counter.count === 9 && counter.enabled === false && counter.stepSize === 2.5
                    && counter.labelText === 'attributes & text';
            })()
            """, "accepted attribute values to refresh the typed JavaScript property getters");
        await RequireStateAsync(page, """
            (() => {
                const root = customElementFixture.counter.shadowRoot;
                return root.querySelector('[data-testid=count]').textContent.trim() === '9'
                    && root.querySelector('[data-testid=enabled]').textContent.trim() === 'false'
                    && root.querySelector('[data-testid=step-size]').textContent.trim() === '2.5'
                    && root.querySelector('[data-testid=label-text]').textContent.trim() === 'attributes & text';
            })()
            """, "one attribute batch to update the ordinary component render");
        Require(await ReadUpdateCountAsync(page) == updatesBeforeAttributes + 1,
            "Changing five attributes synchronously must commit one component update.");

        await page.EvaluateAsync("customElementFixture.counter.setAttribute('count', 'invalid')");
        await page.WaitForTimeoutAsync(100);
        Require(await page.EvaluateAsync<int>("customElementFixture.counter.count") == 9,
            "An invalid integral attribute must preserve its previous typed value.");
        Require(invalidAttributeWarning, "An invalid numeric attribute must report a console warning.");
        await page.EvaluateAsync("customElementFixture.counter.removeAttribute('enabled')");
        await RequireStateAsync(page, "customElementFixture.counter.enabled === false",
            "an absent boolean attribute to remain false");

        int updatesBeforeProperties = await ReadUpdateCountAsync(page);
        await page.Locator("#change-properties").ClickAsync();
        await RequireStateAsync(page, """
            (() => {
                const counter = customElementFixture.counter;
                const root = counter.shadowRoot;
                return counter.count === 12 && counter.enabled === true && counter.stepSize === 0.25
                    && counter.labelText === 'properties & text'
                    && root.querySelector('[data-testid=count]').textContent.trim() === '12'
                    && root.querySelector('[data-testid=label-text]').textContent.trim() === 'properties & text';
            })()
            """, "typed property accessors to share the parameter state");
        Require(await ReadUpdateCountAsync(page) == updatesBeforeProperties + 1,
            "Changing four properties synchronously must commit one component update.");
        await page.Locator("#counter [data-testid=emit-change]").ClickAsync();
        await RequireStateAsync(page, """
            (() => {
                const text = document.getElementById('event-result').textContent;
                if (!text) return false;
                const event = JSON.parse(text);
                return JSON.stringify(event.detail) === '[12.25,true,"properties & text"]'
                    && event.bubbles && event.composed && event.target === 'counter';
            })()
            """, "component Emit to dispatch an ordered, bubbling, composed CustomEvent");

        await VerifyDisconnectAsync(page);
        await page.Locator("#reconnect").ClickAsync();
        await RequireStateAsync(page, """
            customElementFixture.managed.GetMountCount() === 4
                && customElementFixture.counter.shadowRoot?.querySelector('[data-testid=count]')?.textContent.trim() === '12'
                && customElementFixture.counter.shadowRoot.querySelector('slot[name=label]').assignedElements()[0]?.id === 'named-child'
            """, "reconnection to remount with retained parameters and native light children");

        await VerifyAdoptionAsync(page);
        await VerifyDisconnectAsync(page);
        await VerifyMountQueueAsync(page);
    }

    internal static async Task RunStylesheetFallbackAsync(IPage page, Uri address)
    {
        await page.AddInitScriptAsync("globalThis.CSSStyleSheet = undefined");
        await page.GotoAsync(address.AbsoluteUri);
        await RequireStateAsync(page, """
            (() => {
                const root = document.getElementById('counter')?.shadowRoot;
                const link = root?.querySelector('link[rel=stylesheet]');
                const surface = root?.querySelector('.custom-element-surface');
                return link?.href.includes('.viu.css') && surface
                    && getComputedStyle(surface).color === 'rgb(25, 80, 120)';
            })()
            """, "link-clone fallback to apply the bundled stylesheet without constructable sheets");
        await VerifyDisconnectAsync(page);
    }

    private static async Task VerifyDisconnectAsync(IPage page)
    {
        await page.EvaluateAsync("customElementFixture.disconnect()");
        await RequireStateAsync(page, """
            customElementFixture.managed.CaptureRegistry() === customElementFixture.managed.GetEmptyRegistry()
                && customElementFixture.bridge.getRegistrySizes().every(value => value === 0)
                && customElementFixture.counter.shadowRoot.querySelector('[data-testid=emit-change]') === null
            """, "disconnect to release every JavaScript node, listener map, managed listener, and element bridge entry");
        Require(await page.EvaluateAsync<int>("customElementFixture.managed.GetMountCount()")
                == await page.EvaluateAsync<int>("customElementFixture.managed.GetUnmountCount()"),
            "Every mounted custom element must invoke component unmount exactly once.");
    }

    private static async Task VerifyAdoptionAsync(IPage page)
    {
        await page.EvaluateAsync("""
            () => {
                const target = document.getElementById('adoption-document').contentDocument;
                for (const link of document.querySelectorAll('link[rel=stylesheet]')) {
                    const clone = link.cloneNode(true);
                    clone.href = link.href;
                    target.head.append(clone);
                }
                target.body.append(target.adoptNode(customElementFixture.counter));
            }
            """);
        await RequireStateAsync(page, """
            (() => {
                const counter = customElementFixture.counter;
                const target = document.getElementById('adoption-document').contentDocument;
                const root = counter.shadowRoot;
                const surface = root.querySelector('.custom-element-surface');
                return counter.ownerDocument === target && surface
                    && target.defaultView.getComputedStyle(surface).color === 'rgb(25, 80, 120)'
                    && root.querySelector('[data-testid=count]').textContent.trim() === '12'
                    && root.querySelector('slot[name=label]').assignedElements()[0]?.id === 'named-child';
            })()
            """, "adoption into another document to remount and use that document's bundled styles");
    }

    private static async Task VerifyMountQueueAsync(IPage page)
    {
        int mountsBeforeQueue = await page.EvaluateAsync<int>("customElementFixture.managed.GetMountCount()");
        await page.EvaluateAsync("""
            () => {
                customElementFixture.bridge.setReady(false);
                const queued = document.createElement('viu-counter');
                queued.id = 'queued-counter';
                queued.setAttribute('count', '20');
                document.getElementById('elements').append(queued);
                queued.setAttribute('count', '21');
                const abandoned = document.createElement('viu-counter');
                document.getElementById('elements').append(abandoned);
                abandoned.remove();
            }
            """);
        await page.WaitForTimeoutAsync(100);
        Require(await page.EvaluateAsync<int>("customElementFixture.managed.GetMountCount()") == mountsBeforeQueue,
            "Custom elements must not mount until the shared runtime signals readiness.");
        await page.EvaluateAsync("customElementFixture.bridge.setReady(true)");
        await RequireStateAsync(page, """
            document.getElementById('queued-counter').shadowRoot?.querySelector('[data-testid=count]')?.textContent.trim() === '21'
            """, "the ready signal to drain the connected-element queue with its latest parameter values");
        Require(await page.EvaluateAsync<int>("customElementFixture.managed.GetMountCount()") == mountsBeforeQueue + 1,
            "An element removed before readiness must never mount.");
        await page.EvaluateAsync("document.getElementById('queued-counter').remove()");
        await VerifyDisconnectAsync(page);
        await page.EvaluateAsync("customElementFixture.managed.DisposeOwner()");
    }

    private static Task<int> ReadUpdateCountAsync(IPage page) =>
        page.EvaluateAsync<int>("customElementFixture.managed.GetUpdateCount()");

    private static async Task RequireStateAsync(IPage page, string expression, string description)
    {
        try
        {
            await page.WaitForFunctionAsync(expression, options: new PageWaitForFunctionOptions { Timeout = 30_000 });
        }
        catch (PlaywrightException exception)
        {
            throw new InvalidOperationException($"Timed out waiting for {description}.", exception);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
