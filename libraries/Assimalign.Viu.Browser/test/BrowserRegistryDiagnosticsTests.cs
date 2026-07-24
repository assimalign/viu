using System;
using System.Runtime.Versioning;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Browser.Tests;

/// <summary>
/// Tests selection, dispatch routing, and restoration of the active event registry shared by
/// diagnostics and the browser event export.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class BrowserRegistryDiagnosticsTests
{
    [Fact]
    public void ActiveInvokerCount_WithNoBufferedRenderer_SelectsDirectRegistry()
    {
        BrowserNodeOperations.OverrideInvokerRegistry.ShouldBeNull();

        BrowserNodeOperations.ActiveInvokerCount.ShouldBe(
            BrowserNodeOperations.DirectInvokerCount);
    }

    [Fact]
    public void ActiveInvokerCount_WithBufferedRenderer_SelectsBufferedRegistryAndRestoresDirectRegistry()
    {
        int directInvokerCount = BrowserNodeOperations.DirectInvokerCount;
        BufferedBrowserNodeOperations buffered = CreateBufferedOperations();
        buffered.Invokers.SetListener(
            42,
            "onClick",
            (Action)(static () => { }));

        BrowserNodeOperations.ActiveInvokerCount.ShouldBe(directInvokerCount);

        try
        {
            buffered.Activate();

            BrowserNodeOperations.ActiveInvokerCount.ShouldBe(1);

            buffered.Invokers.PurgeReleasedHandles([42]);

            BrowserNodeOperations.ActiveInvokerCount.ShouldBe(0);
        }
        finally
        {
            buffered.Deactivate();
        }

        BrowserNodeOperations.OverrideInvokerRegistry.ShouldBeNull();
        BrowserNodeOperations.ActiveInvokerCount.ShouldBe(directInvokerCount);
    }

    [Fact]
    public void DispatchEvent_WithBufferedRenderer_RoutesPropertyAndModelChannelsThroughTheActiveRegistry()
    {
        const int element = 43;
        int propertyInvocationCount = 0;
        int modelInvocationCount = 0;
        string? modelValue = null;
        BufferedBrowserNodeOperations buffered = CreateBufferedOperations();
        buffered.Invokers.SetListener(
            element,
            "onInput",
            (Action)(() => propertyInvocationCount++));
        buffered.Invokers.SetModelListener(
            element,
            "onInput",
            (Action<BrowserEvent>)(browserEvent =>
            {
                modelInvocationCount++;
                modelValue = browserEvent.TargetValue;
            }));

        try
        {
            buffered.Activate();

            int responseFlags = BrowserNodeOperations.DispatchEvent(
                element,
                capture: false,
                CreateInputEvent("edited"));

            responseFlags.ShouldBe(0);
            propertyInvocationCount.ShouldBe(1);
            modelInvocationCount.ShouldBe(1);
            modelValue.ShouldBe("edited");
            BrowserNodeOperations.ActiveInvokerCount.ShouldBe(1);
        }
        finally
        {
            buffered.Deactivate();
        }

        BrowserNodeOperations.OverrideInvokerRegistry.ShouldBeNull();
    }

    private static BufferedBrowserNodeOperations CreateBufferedOperations()
    {
        return new BufferedBrowserNodeOperations(
            static (_, _) => [],
            static _ => 0,
            static _ => 0,
            static _ => 0,
            static (_, _, _, _) => (0, 0));
    }

    private static BrowserEvent CreateInputEvent(string targetValue)
    {
        return new BrowserEvent(
            "input",
            1000,
            string.Empty,
            string.Empty,
            BrowserEventModifiers.None,
            -1,
            0,
            0,
            0,
            0,
            true,
            targetValue,
            false);
    }
}
