using System;
using System.IO;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Browser.Tests;

// [CEL-1]–[CEL-8]: source-level boundary contracts; real lifecycle behavior runs in the packaged
// Chromium fixture. Native contracts: https://html.spec.whatwg.org/multipage/custom-elements.html
// and https://dom.spec.whatwg.org/.
public sealed class CustomElementsJavaScriptContractTests
{
    [Fact]
    public void Define_UsesOneNativeFactoryWithGeneratedObservedAttributesAndFourLifecycleCallbacks()
    {
        string source = ReadBridge();

        CountOccurrences(source, "customElements.define(tagName, class extends HTMLElement").ShouldBe(1);
        source.ShouldContain("static get observedAttributes() { return Array.from(definition.attributes.keys()) }");
        source.ShouldContain("connectedCallback()");
        source.ShouldContain("disconnectedCallback()");
        source.ShouldContain("attributeChangedCallback(attributeName, previousValue, value)");
        source.ShouldContain("adoptedCallback()");
        source.ShouldContain("Object.defineProperty(this, name");
        source.ShouldContain("customElements.get(tagName)");
        source.ShouldContain("is already defined");
        source.ShouldContain("name in HTMLElement.prototype");
        source.ShouldContain("customElementLifecycleNames.has(name)");
        source.ShouldContain("conflicts with a native element member");
        source.ShouldNotContain("new Function(");
        source.ShouldNotContain("eval(");
    }

    [Fact]
    public void LifecycleBatch_QueuesUntilReadyAndCrossesOnceWithParallelPrimitiveArrays()
    {
        string source = ReadBridge();

        source.ShouldContain("let customElementsReady = false");
        source.ShouldContain("enqueueMicrotask(flushCustomElementBatch)");
        source.ShouldContain("if (!customElementsReady) continue");
        CountOccurrences(source,
            "dispatchCustomElements(operations, definitionIdentifiers, elementIdentifiers, containerHandles, names, values)")
            .ShouldBe(1);
        source.ShouldContain("BrowserCustomElementDispatch.Dispatch");
        source.ShouldContain("if (!state.connected && !state.mounted)");
        source.ShouldContain("pendingCustomElements.delete(state)");
    }

    [Fact]
    public void UpdateProperties_EveryManagedValueRefreshesGetterWithoutChangingItsInputSource()
    {
        string source = ReadBridge();
        int start = source.IndexOf("updateProperties: (elementIdentifier, names, values) =>", StringComparison.Ordinal);
        int end = source.IndexOf("dispatchEvent: (elementIdentifier, name, argumentsList)", start, StringComparison.Ordinal);
        string updateProperties = source[start..end];
        int publicationLoop = updateProperties.IndexOf(
            "for (let index = 0; index < names.length; index++)",
            StringComparison.Ordinal);
        int getterPublication = updateProperties.IndexOf(
            "state.properties.set(name, values[index])",
            StringComparison.Ordinal);
        int propertyInputLookup = updateProperties.IndexOf(
            "const input = state.inputs.get(name)",
            StringComparison.Ordinal);

        publicationLoop.ShouldBeGreaterThan(-1);
        getterPublication.ShouldBeGreaterThan(publicationLoop);
        propertyInputLookup.ShouldBeGreaterThan(getterPublication);
        updateProperties[publicationLoop..propertyInputLookup].ShouldNotContain("if (");
        updateProperties.ShouldContain("if (input?.operation === 4) input.value = values[index]");
        updateProperties.ShouldNotContain("state.inputs.set(");
    }

    [Fact]
    public void Slots_DiscoverDirectSlottablesPreserveFallbackAndCoalesceOperationSix()
    {
        string source = ReadBridge();
        int mountStart = source.IndexOf("if (!state.mounted)", StringComparison.Ordinal);
        int mountEnd = source.IndexOf("append(state, 1, state.slotNames)", mountStart, StringComparison.Ordinal);
        string mount = source[mountStart..mountEnd];

        source.ShouldContain("const names = new Set()");
        source.ShouldContain("for (const child of element.childNodes)");
        source.ShouldContain("child.nodeType === Node.TEXT_NODE");
        source.ShouldContain("child.nodeType === Node.ELEMENT_NODE");
        source.ShouldContain("names.add(child.getAttribute('slot') || '')");
        source.ShouldNotContain("const names = new Set([''])");
        source.ShouldContain("state.slotObserver = new MutationObserver(records =>");
        source.ShouldContain("record.target === state.element");
        source.ShouldContain("record.target.parentNode === state.element");
        source.ShouldContain("attributeFilter: ['slot']");
        source.ShouldContain("state.changes.find(change => change.operation === 6)");
        source.ShouldContain("state.changes.push({ operation: 6, name: slotNames, value: null })");
        source.ShouldContain("disconnectCustomElementSlotObserver(state)");
        source.ShouldContain("append(state, 1, state.slotNames)");
        mount.ShouldContain("observeCustomElementSlots(state)");
    }

    [Fact]
    public void ShadowStyles_ShareDocumentSheetsPreserveLinkFallbackAndInvalidateAsyncWorkOnDisconnect()
    {
        string source = ReadBridge();

        source.ShouldContain("const customElementStyleDocuments = new WeakMap()");
        source.ShouldContain("sheet = cache.sheets.get(key)");
        source.ShouldContain("link.hasAttribute('data-viu-stylesheet')");
        source.ShouldContain("const clone = link.cloneNode(false)");
        source.ShouldContain("clone.href = link.href");
        source.ShouldContain("current.root.adoptedStyleSheets = [...current.root.adoptedStyleSheets, ...sheets]");
        source.ShouldContain("current.styleVersion !== version");
        source.ShouldContain("typeof globalThis.WeakRef !== 'function'");
        source.ShouldContain("new globalThis.WeakRef(state)");
        // One declaration plus the head-observer refresh and mount-time refresh calls.
        CountOccurrences(source, "pruneCustomElementStylesheetCache(ownerDocument, cache)").ShouldBe(3);
        source.ShouldContain("if (!currentKeys.has(key)) cache.sheets.delete(key)");
        source.ShouldContain("if (cache.sheets.get(key) === sheet) cache.sheets.delete(key)");
        source.ShouldContain("cache.observer?.disconnect()");
    }

    [Fact]
    public void Events_ForwardOrderedArgumentListAsBubblingComposedCustomEvent()
    {
        string source = ReadBridge();

        source.ShouldContain("dispatchEvent: (elementIdentifier, name, argumentsList)");
        source.ShouldContain("detail: argumentsList, bubbles: true, composed: true");
        source.ShouldContain("state.element.ownerDocument.defaultView?.CustomEvent");
        source.ShouldContain("warn: message => console.warn('[Viu warn] ' + message)");
    }

    [Fact]
    public void Release_ReleasesShadowFragmentAndHostHandlesWithoutReleasingAuthoredLightChildren()
    {
        string source = ReadBridge();
        int start = source.IndexOf("release: elementIdentifier =>", StringComparison.Ordinal);
        int end = source.IndexOf("undefine: definitionIdentifier =>", start, StringComparison.Ordinal);
        string release = source[start..end];

        release.ShouldContain("detachCustomElementStyles(state)");
        release.ShouldContain("if (state.root !== state.element)");
        release.ShouldContain("createTreeWalker(state.root, NodeFilter.SHOW_ALL)");
        release.ShouldContain("releaseNodeHandle(state.root, released)");
        release.ShouldContain("releaseNodeHandle(state.element, released)");
        release.ShouldContain("activeCustomElements.delete(elementIdentifier)");
        release.ShouldContain("pendingCustomElements.delete(state)");
        release.ShouldNotContain("releaseSubtree(state.element");
        release.ShouldNotContain("state.element.remove(");
        release.ShouldNotContain("state.element.textContent");
    }

    private static int CountOccurrences(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;

    private static string ReadBridge()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string path = Path.Combine(directory.FullName, "src", "wwwroot", "viu-dom.js");
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate Browser's shipping viu-dom.js from the test output.");
    }
}
