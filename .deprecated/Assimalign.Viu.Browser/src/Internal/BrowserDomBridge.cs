using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace Assimalign.Viu.Browser;

/// <summary>
/// The C# half of the DOM interop bridge: <c>[JSImport]</c> bindings to the package's
/// <c>viu-dom.js</c> module plus the wrapper layer that converts structured JS failures
/// (<c>viu-dom|operation|handle|message</c>) into typed <see cref="BrowserDomException"/>s.
/// Signatures stay primitive (int handles, strings, bools) so every op remains expressible as
/// an opcode for the command buffer ([V01.01.04.05]). Single-threaded by design — never call
/// off the main WASM thread.
/// </summary>
[SupportedOSPlatform("browser")]
internal static partial class BrowserDomBridge
{
    /// <summary>The JS module name; must match <c>BrowserRuntime</c>'s import.</summary>
    internal const string ModuleName = "Assimalign.Viu.Browser";

    internal static System.Threading.Tasks.Task InitializeModuleAsync()
        => Imports.Initialize();

    internal static int QuerySelector(string selector)
    {
        try
        {
            return Imports.QuerySelector(selector);
        }
        catch (JSException exception)
        {
            throw Translate("querySelector", 0, exception);
        }
    }

    internal static int CreateElement(string tagName, string? namespaceName)
    {
        try
        {
            return Imports.CreateElement(tagName, namespaceName);
        }
        catch (JSException exception)
        {
            throw Translate("createElement", 0, exception);
        }
    }

    internal static int CreateText(string text)
    {
        try
        {
            return Imports.CreateText(text);
        }
        catch (JSException exception)
        {
            throw Translate("createText", 0, exception);
        }
    }

    internal static int CreateComment(string text)
    {
        try
        {
            return Imports.CreateComment(text);
        }
        catch (JSException exception)
        {
            throw Translate("createComment", 0, exception);
        }
    }

    internal static void SetText(int nodeHandle, string text)
    {
        try
        {
            Imports.SetText(nodeHandle, text);
        }
        catch (JSException exception)
        {
            throw Translate("setText", nodeHandle, exception);
        }
    }

    internal static int[] SetElementText(int nodeHandle, string text)
    {
        try
        {
            return Imports.SetElementText(nodeHandle, text);
        }
        catch (JSException exception)
        {
            throw Translate("setElementText", nodeHandle, exception);
        }
    }

    internal static void Insert(int parentHandle, int childHandle, int anchorHandle)
    {
        try
        {
            Imports.Insert(parentHandle, childHandle, anchorHandle);
        }
        catch (JSException exception)
        {
            throw Translate("insert", parentHandle, exception);
        }
    }

    internal static int[] Remove(int childHandle)
    {
        try
        {
            return Imports.Remove(childHandle);
        }
        catch (JSException exception)
        {
            throw Translate("remove", childHandle, exception);
        }
    }

    internal static int ParentNode(int nodeHandle)
    {
        try
        {
            return Imports.ParentNode(nodeHandle);
        }
        catch (JSException exception)
        {
            throw Translate("parentNode", nodeHandle, exception);
        }
    }

    internal static int NextSibling(int nodeHandle)
    {
        try
        {
            return Imports.NextSibling(nodeHandle);
        }
        catch (JSException exception)
        {
            throw Translate("nextSibling", nodeHandle, exception);
        }
    }

    internal static string SnapshotHydration(int containerHandle)
    {
        try
        {
            return Imports.SnapshotHydration(containerHandle);
        }
        catch (JSException exception)
        {
            throw Translate("snapshotHydration", containerHandle, exception);
        }
    }

    internal static int[] InsertStaticContent(string content, int parentHandle, int anchorHandle, string? namespaceName)
    {
        try
        {
            return Imports.InsertStaticContent(content, parentHandle, anchorHandle, namespaceName);
        }
        catch (JSException exception)
        {
            throw Translate("insertStaticContent", parentHandle, exception);
        }
    }

    internal static void SetAttribute(int nodeHandle, string name, string value)
    {
        try
        {
            Imports.SetAttribute(nodeHandle, name, value);
        }
        catch (JSException exception)
        {
            throw Translate("setAttribute", nodeHandle, exception);
        }
    }

    internal static void RemoveAttribute(int nodeHandle, string name)
    {
        try
        {
            Imports.RemoveAttribute(nodeHandle, name);
        }
        catch (JSException exception)
        {
            throw Translate("removeAttribute", nodeHandle, exception);
        }
    }

    internal static void SetXlinkAttribute(int nodeHandle, string name, string value)
    {
        try
        {
            Imports.SetXlinkAttribute(nodeHandle, name, value);
        }
        catch (JSException exception)
        {
            throw Translate("setXlinkAttribute", nodeHandle, exception);
        }
    }

    internal static void RemoveXlinkAttribute(int nodeHandle, string name)
    {
        try
        {
            Imports.RemoveXlinkAttribute(nodeHandle, name);
        }
        catch (JSException exception)
        {
            throw Translate("removeXlinkAttribute", nodeHandle, exception);
        }
    }

    internal static void SetClassName(int nodeHandle, string value)
    {
        try
        {
            Imports.SetClassName(nodeHandle, value);
        }
        catch (JSException exception)
        {
            throw Translate("setClassName", nodeHandle, exception);
        }
    }

    internal static void SetStringProperty(int nodeHandle, string name, string value)
    {
        try
        {
            Imports.SetStringProperty(nodeHandle, name, value);
        }
        catch (JSException exception)
        {
            throw Translate("setStringProperty", nodeHandle, exception);
        }
    }

    internal static void SetBooleanProperty(int nodeHandle, string name, bool value)
    {
        try
        {
            Imports.SetBooleanProperty(nodeHandle, name, value);
        }
        catch (JSException exception)
        {
            throw Translate("setBooleanProperty", nodeHandle, exception);
        }
    }

    internal static void SetValueGuarded(int nodeHandle, string value)
    {
        try
        {
            Imports.SetValueGuarded(nodeHandle, value);
        }
        catch (JSException exception)
        {
            throw Translate("setValueGuarded", nodeHandle, exception);
        }
    }

    internal static void SetStyleText(int nodeHandle, string cssText)
    {
        try
        {
            Imports.SetStyleText(nodeHandle, cssText);
        }
        catch (JSException exception)
        {
            throw Translate("setStyleText", nodeHandle, exception);
        }
    }

    internal static void SetStyleProperty(int nodeHandle, string name, string value, bool important)
    {
        try
        {
            Imports.SetStyleProperty(nodeHandle, name, value, important);
        }
        catch (JSException exception)
        {
            throw Translate("setStyleProperty", nodeHandle, exception);
        }
    }

    internal static void RemoveStyleProperty(int nodeHandle, string name)
    {
        try
        {
            Imports.RemoveStyleProperty(nodeHandle, name);
        }
        catch (JSException exception)
        {
            throw Translate("removeStyleProperty", nodeHandle, exception);
        }
    }

    internal static void SetCssVariables(int nodeHandle, string[] names, string[] values)
    {
        try
        {
            Imports.SetCssVariables(nodeHandle, names, values);
        }
        catch (JSException exception)
        {
            throw Translate("setCssVars", nodeHandle, exception);
        }
    }

    internal static void AddEventListener(int nodeHandle, string eventName, bool once, bool capture, bool passive)
    {
        try
        {
            Imports.AddEventListener(nodeHandle, eventName, once, capture, passive);
        }
        catch (JSException exception)
        {
            throw Translate("addEventListener", nodeHandle, exception);
        }
    }

    internal static void RemoveEventListener(int nodeHandle, string eventName, bool capture)
    {
        try
        {
            Imports.RemoveEventListener(nodeHandle, eventName, capture);
        }
        catch (JSException exception)
        {
            throw Translate("removeEventListener", nodeHandle, exception);
        }
    }

    internal static int[] GetRegistrySizes()
    {
        try
        {
            return Imports.GetRegistrySizes();
        }
        catch (JSException exception)
        {
            throw Translate("getRegistrySizes", 0, exception);
        }
    }

    // --- transitions ([V01.01.04.07]) -----------------------------------------------------------
    // classList add/remove, the double-rAF next frame, the forced reflow, transition-end detection,
    // and the FLIP getBoundingClientRect/transform ops. The rAF/listener scheduling stays JS-side and
    // calls a single .NET Action back per completion (never a per-property managed listener). These
    // ops are inherently rAF-timed and read-then-write, so they use direct interop rather than the
    // batched command buffer — the DOM-side contract the DomTransitionOperations abstraction requires.

    internal static void AddTransitionClass(int nodeHandle, string cssClass)
    {
        try
        {
            Imports.AddTransitionClass(nodeHandle, cssClass);
        }
        catch (JSException exception)
        {
            throw Translate("addTransitionClass", nodeHandle, exception);
        }
    }

    internal static void RemoveTransitionClass(int nodeHandle, string cssClass)
    {
        try
        {
            Imports.RemoveTransitionClass(nodeHandle, cssClass);
        }
        catch (JSException exception)
        {
            throw Translate("removeTransitionClass", nodeHandle, exception);
        }
    }

    internal static void NextFrame(Action callback)
    {
        try
        {
            Imports.NextFrame(callback);
        }
        catch (JSException exception)
        {
            throw Translate("nextFrame", 0, exception);
        }
    }

    internal static void ForceReflow()
    {
        try
        {
            Imports.ForceReflow();
        }
        catch (JSException exception)
        {
            throw Translate("forceReflow", 0, exception);
        }
    }

    internal static void WhenTransitionEnds(int nodeHandle, string? expectedType, int explicitTimeout, Action resolve)
    {
        try
        {
            Imports.WhenTransitionEnds(nodeHandle, expectedType, explicitTimeout, resolve);
        }
        catch (JSException exception)
        {
            throw Translate("whenTransitionEnds", nodeHandle, exception);
        }
    }

    internal static double[] MeasurePositions(int[] nodeHandles)
    {
        try
        {
            return Imports.MeasurePositions(nodeHandles);
        }
        catch (JSException exception)
        {
            throw Translate("measurePositions", nodeHandles.Length > 0 ? nodeHandles[0] : 0, exception);
        }
    }

    internal static void SetMoveTransform(int nodeHandle, double deltaX, double deltaY)
    {
        try
        {
            Imports.SetMoveTransform(nodeHandle, deltaX, deltaY);
        }
        catch (JSException exception)
        {
            throw Translate("setMoveTransform", nodeHandle, exception);
        }
    }

    internal static void ClearMoveStyles(int nodeHandle)
    {
        try
        {
            Imports.ClearMoveStyles(nodeHandle);
        }
        catch (JSException exception)
        {
            throw Translate("clearMoveStyles", nodeHandle, exception);
        }
    }

    internal static bool HasCssTransform(int nodeHandle, int rootHandle, string moveClass)
    {
        try
        {
            return Imports.HasCssTransform(nodeHandle, rootHandle, moveClass);
        }
        catch (JSException exception)
        {
            throw Translate("hasCssTransform", nodeHandle, exception);
        }
    }

    internal static void WhenMoveEnds(int nodeHandle, Action resolve)
    {
        try
        {
            Imports.WhenMoveEnds(nodeHandle, resolve);
        }
        catch (JSException exception)
        {
            throw Translate("whenMoveEnds", nodeHandle, exception);
        }
    }

    // The command-buffer apply ([V01.01.04.05]): the whole batched op frame crosses the boundary
    // once per flush as a MemoryView over WASM memory (no copy of the argument), and the applier
    // returns every handle it released while draining the batch so the .NET side purges its invoker
    // delegates in the same single call — the batched analogue of remove/setElementText's per-op
    // released-handle return.
    internal static int[] ApplyCommandBuffer(byte[] frame, int length)
    {
        try
        {
            return Imports.ApplyCommandBuffer(frame.AsSpan(0, length));
        }
        catch (JSException exception)
        {
            throw Translate("applyCommandBuffer", 0, exception);
        }
    }

    // Takes Exception (not JSException) so the pure parsing is testable on the CoreCLR host —
    // JSException cannot even be constructed off-browser.
    internal static BrowserDomException Translate(string operationName, int nodeHandle, Exception exception)
    {
        // Structured bridge errors: "viu-dom|operation|handle|message".
        var message = exception.Message;
        if (message.StartsWith("viu-dom|", StringComparison.Ordinal))
        {
            var parts = message.Split('|', 4);
            if (parts.Length == 4 && int.TryParse(parts[2], out var reportedHandle))
            {
                return new BrowserDomException(parts[1], reportedHandle, parts[3], exception);
            }
        }
        return new BrowserDomException(operationName, nodeHandle, message, exception);
    }

    // --- raw imports (module: Assimalign.Viu.Browser -> wwwroot/viu-dom.js) ------------

    private static partial class Imports
    {
        [JSImport("dom.querySelector", ModuleName)]
        internal static partial int QuerySelector(string selector);

        [JSImport("dom.createElement", ModuleName)]
        internal static partial int CreateElement(string tagName, string? namespaceName);

        [JSImport("dom.createText", ModuleName)]
        internal static partial int CreateText(string textContent);

        [JSImport("dom.createComment", ModuleName)]
        internal static partial int CreateComment(string textContent);

        [JSImport("dom.setText", ModuleName)]
        internal static partial void SetText(int nodeHandle, string textContent);

        [JSImport("dom.setElementText", ModuleName)]
        internal static partial int[] SetElementText(int nodeHandle, string textContent);

        [JSImport("dom.insert", ModuleName)]
        internal static partial void Insert(int parentHandle, int childHandle, int anchorHandle);

        [JSImport("dom.remove", ModuleName)]
        internal static partial int[] Remove(int childHandle);

        [JSImport("dom.parentNode", ModuleName)]
        internal static partial int ParentNode(int nodeHandle);

        [JSImport("dom.nextSibling", ModuleName)]
        internal static partial int NextSibling(int nodeHandle);

        [JSImport("dom.snapshotHydration", ModuleName)]
        internal static partial string SnapshotHydration(int containerHandle);

        [JSImport("dom.insertStaticContent", ModuleName)]
        internal static partial int[] InsertStaticContent(string content, int parentHandle, int anchorHandle, string? namespaceName);

        [JSImport("dom.setAttribute", ModuleName)]
        internal static partial void SetAttribute(int nodeHandle, string name, string value);

        [JSImport("dom.removeAttribute", ModuleName)]
        internal static partial void RemoveAttribute(int nodeHandle, string name);

        [JSImport("dom.setXlinkAttribute", ModuleName)]
        internal static partial void SetXlinkAttribute(int nodeHandle, string name, string value);

        [JSImport("dom.removeXlinkAttribute", ModuleName)]
        internal static partial void RemoveXlinkAttribute(int nodeHandle, string name);

        [JSImport("dom.setClassName", ModuleName)]
        internal static partial void SetClassName(int nodeHandle, string value);

        [JSImport("dom.setStringProperty", ModuleName)]
        internal static partial void SetStringProperty(int nodeHandle, string name, string value);

        [JSImport("dom.setBooleanProperty", ModuleName)]
        internal static partial void SetBooleanProperty(int nodeHandle, string name, bool value);

        [JSImport("dom.setValueGuarded", ModuleName)]
        internal static partial void SetValueGuarded(int nodeHandle, string value);

        [JSImport("dom.setStyleText", ModuleName)]
        internal static partial void SetStyleText(int nodeHandle, string cssText);

        [JSImport("dom.setStyleProperty", ModuleName)]
        internal static partial void SetStyleProperty(int nodeHandle, string name, string value, bool important);

        [JSImport("dom.removeStyleProperty", ModuleName)]
        internal static partial void RemoveStyleProperty(int nodeHandle, string name);

        [JSImport("dom.setCssVars", ModuleName)]
        internal static partial void SetCssVariables(
            int nodeHandle,
            [JSMarshalAs<JSType.Array<JSType.String>>] string[] names,
            [JSMarshalAs<JSType.Array<JSType.String>>] string[] values);

        [JSImport("dom.addEventListener", ModuleName)]
        internal static partial void AddEventListener(int nodeHandle, string eventName, bool once, bool capture, bool passive);

        [JSImport("dom.removeEventListener", ModuleName)]
        internal static partial void RemoveEventListener(int nodeHandle, string eventName, bool capture);

        [JSImport("dom.getRegistrySizes", ModuleName)]
        internal static partial int[] GetRegistrySizes();

        [JSImport("dom.applyCommandBuffer", ModuleName)]
        internal static partial int[] ApplyCommandBuffer([JSMarshalAs<JSType.MemoryView>] Span<byte> buffer);

        [JSImport("dom.addTransitionClass", ModuleName)]
        internal static partial void AddTransitionClass(int nodeHandle, string cssClass);

        [JSImport("dom.removeTransitionClass", ModuleName)]
        internal static partial void RemoveTransitionClass(int nodeHandle, string cssClass);

        [JSImport("dom.nextFrame", ModuleName)]
        internal static partial void NextFrame([JSMarshalAs<JSType.Function>] Action callback);

        [JSImport("dom.forceReflow", ModuleName)]
        internal static partial void ForceReflow();

        [JSImport("dom.whenTransitionEnds", ModuleName)]
        internal static partial void WhenTransitionEnds(
            int nodeHandle,
            string? expectedType,
            int explicitTimeout,
            [JSMarshalAs<JSType.Function>] Action resolve);

        // Batched FLIP snapshot read ([V01.01.04.07.03]): the handle array crosses in one call and the
        // flat [left, top, ...] result returns in one call, mirroring the history bridge's readSnapshot
        // flat-primitives pattern — N children cost one crossing, not N.
        [JSImport("dom.measurePositions", ModuleName)]
        [return: JSMarshalAs<JSType.Array<JSType.Number>>]
        internal static partial double[] MeasurePositions(
            [JSMarshalAs<JSType.Array<JSType.Number>>] int[] nodeHandles);

        [JSImport("dom.setMoveTransform", ModuleName)]
        internal static partial void SetMoveTransform(int nodeHandle, double deltaX, double deltaY);

        [JSImport("dom.clearMoveStyles", ModuleName)]
        internal static partial void ClearMoveStyles(int nodeHandle);

        [JSImport("dom.hasCssTransform", ModuleName)]
        internal static partial bool HasCssTransform(int nodeHandle, int rootHandle, string moveClass);

        [JSImport("dom.whenMoveEnds", ModuleName)]
        internal static partial void WhenMoveEnds(int nodeHandle, [JSMarshalAs<JSType.Function>] Action resolve);

        [JSImport("initialize", ModuleName)]
        [return: JSMarshalAs<JSType.Promise<JSType.Void>>]
        internal static partial System.Threading.Tasks.Task Initialize();
    }
}
