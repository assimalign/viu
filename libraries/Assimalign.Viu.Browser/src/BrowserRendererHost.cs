using System;
using System.ComponentModel;
using System.Runtime.Versioning;

using Assimalign.Viu;

namespace Assimalign.Viu.Browser;

/// <summary>
/// Owns a buffered Browser implementation of <see cref="RendererOptions{TNode}"/> over an
/// injected command-frame boundary.
/// </summary>
/// <remarks>
/// This low-level host exists for embedding and DOM-less command capture. Managed handles are
/// allocated before a frame is applied; the default handle is reserved for no node. The host is
/// single-threaded and is not thread-safe. Specified by <c>[RND-HOST-1]</c> through
/// <c>[RND-HOST-4]</c> and <c>[RND-IO-1]</c> through <c>[RND-IO-4]</c>.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class BrowserRendererHost
{
    private readonly BufferedBrowserNodeOperations _operations;

    internal BufferedBrowserNodeOperations Operations => _operations;

    /// <summary>Initializes a DOM-less buffered host over a synchronous frame applier.</summary>
    /// <param name="applyCommandFrame">
    /// The operation that applies one complete frame and reports released handles.
    /// </param>
    /// <param name="parentNode">
    /// The optional forced-flush parent read; omitted reads return the no-node sentinel.
    /// </param>
    /// <param name="nextSibling">
    /// The optional forced-flush next-sibling read; omitted reads return the no-node sentinel.
    /// </param>
    /// <param name="snapshotHydration">
    /// The optional one-call serialized hydration snapshot operation.
    /// </param>
    public BrowserRendererHost(
        BrowserCommandFrameApplier applyCommandFrame,
        Func<int, int>? parentNode = null,
        Func<int, int>? nextSibling = null,
        Func<int, string>? snapshotHydration = null)
        : this(
            applyCommandFrame,
            parentNode,
            nextSibling,
            snapshotHydration,
            scheduleHydrationTrigger: null)
    {
    }

    /// <summary>
    /// Initializes a DOM-less buffered host with explicit hydration snapshot and trigger seams.
    /// </summary>
    /// <param name="applyCommandFrame">
    /// The operation that applies one complete frame and reports released handles.
    /// </param>
    /// <param name="parentNode">The nullable forced-flush parent read.</param>
    /// <param name="nextSibling">The nullable forced-flush next-sibling read.</param>
    /// <param name="snapshotHydration">The nullable serialized hydration snapshot operation.</param>
    /// <param name="scheduleHydrationTrigger">
    /// The nullable host-neutral deferred-hydration trigger seam.
    /// </param>
    /// <remarks>
    /// Trigger delivery and interaction replay must be asynchronous, and disposing the returned
    /// registration must release all host observers and listeners. Specified by
    /// <c>[HYD-LAZY-3]</c> through <c>[HYD-LAZY-5]</c>.
    /// </remarks>
    public BrowserRendererHost(
        BrowserCommandFrameApplier applyCommandFrame,
        Func<int, int>? parentNode,
        Func<int, int>? nextSibling,
        Func<int, string>? snapshotHydration,
        Func<HydrationTriggerRequest<int>, IHydrationTriggerRegistration>?
            scheduleHydrationTrigger)
    {
        ArgumentNullException.ThrowIfNull(applyCommandFrame);
        _operations = new BufferedBrowserNodeOperations(
            applyCommandFrame.Invoke,
            querySelector: null,
            parentNode ?? (static _ => 0),
            nextSibling ?? (static _ => 0),
            insertStaticContent: null,
            snapshotHydration,
            scheduleHydrationTrigger);
        Options = _operations.Create();
    }

    /// <summary>Gets the complete Core renderer contract backed by this buffered host.</summary>
    public RendererOptions<int> Options { get; }

    /// <summary>Gets the number of non-empty command frames applied by this host.</summary>
    public int InteropCallCount => _operations.InteropCallCount;

    /// <summary>
    /// Exclusively routes Browser events, directives, and transitions through this host until the
    /// returned lease is disposed.
    /// </summary>
    /// <returns>The activation lease. Dispose it before activating another Browser host.</returns>
    /// <exception cref="InvalidOperationException">Another Browser host is already active.</exception>
    [SupportedOSPlatform("browser")]
    public IDisposable Activate() => _operations.Activate();

    /// <summary>Advances managed allocation beyond an externally issued nonzero handle.</summary>
    /// <param name="handle">The foreign host handle.</param>
    public void ObserveForeignHandle(int handle) => _operations.ObserveForeignHandle(handle);

    /// <summary>
    /// Dispatches a DOM-less event through the listeners owned by this host. The host must remain
    /// active while directive listeners execute.
    /// </summary>
    /// <param name="nodeHandle">The element handle whose listener receives the event.</param>
    /// <param name="capture">Whether the capture-phase listener receives the event.</param>
    /// <param name="browserEvent">The immutable Browser event fields and mutable response intents.</param>
    [SupportedOSPlatform("browser")]
    public void DispatchEvent(
        int nodeHandle,
        bool capture,
        BrowserEvent browserEvent)
    {
        ArgumentNullException.ThrowIfNull(browserEvent);
        _ = _operations.DispatchEvent(nodeHandle, capture, browserEvent);
    }
}
