using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

using Assimalign.Viu.State;

namespace Assimalign.Viu.Browser;

/// <summary>
/// Stores complete State persistence payloads in the selected browser storage area, with one
/// interop crossing per read, write, or removal. Specified by <c>[STA-11]</c>.
/// </summary>
/// <remarks>
/// Uses the <see href="https://html.spec.whatwg.org/multipage/webstorage.html">WHATWG Web Storage
/// standard</see>. Browser application startup initializes the bridge before component setup;
/// earlier storage access requires awaiting <see cref="BrowserRuntime.InitializeAsync"/>.
/// Storage failures propagate to the caller so the persistence plugin can report diagnostics.
/// Single-threaded by design: call only on the browser main thread; not thread-safe.
/// </remarks>
[SupportedOSPlatform("browser")]
public sealed class BrowserStateStorage : IStateStorage
{
    /// <summary>
    /// Selects the storage area without accessing JavaScript or allocating a browser handle.
    /// Specified by <c>[STA-11]</c>.
    /// </summary>
    /// <param name="kind">The local or session storage area used by every operation.</param>
    /// <exception cref="ArgumentOutOfRangeException">The storage kind is not defined.</exception>
    public BrowserStateStorage(StateStorageKind kind = StateStorageKind.Local)
    {
        if (kind is not StateStorageKind.Local and not StateStorageKind.Session)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown state storage kind.");
        }

        Kind = kind;
    }

    /// <summary>
    /// Gets the storage area selected at construction; it never changes. Specified by <c>[STA-11]</c>.
    /// </summary>
    public StateStorageKind Kind { get; }

    /// <summary>
    /// Reads one complete payload in one interop crossing, preserving an existing empty string
    /// as a successful read. Specified by <c>[STA-11]</c>.
    /// </summary>
    /// <param name="key">The exact storage key; empty keys are supported by Web Storage.</param>
    /// <param name="value">The stored string, or an empty string when the key is absent.</param>
    /// <returns>Whether the key exists in the selected storage area.</returns>
    /// <exception cref="ArgumentNullException">The key is null.</exception>
    /// <exception cref="InvalidOperationException">The browser bridge is not initialized.</exception>
    /// <exception cref="JSException">Browser policy prevents storage access.</exception>
    public bool TryRead(string key, out string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        BrowserRuntime.EnsureBridgeInitialized();
        string? storedValue = BrowserStateStorageInterop.Read(Kind == StateStorageKind.Session, key);
        value = storedValue ?? string.Empty;
        return storedValue is not null;
    }

    /// <summary>
    /// Writes one complete payload in one interop crossing. Quota and security failures propagate
    /// to the persistence plugin's diagnostic boundary. Specified by <c>[STA-11]</c>.
    /// </summary>
    /// <param name="key">The exact storage key.</param>
    /// <param name="value">The complete string payload.</param>
    /// <exception cref="ArgumentNullException">The key or value is null.</exception>
    /// <exception cref="InvalidOperationException">The browser bridge is not initialized.</exception>
    /// <exception cref="JSException">The browser rejects the write, including quota or security failures.</exception>
    public void Write(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        BrowserRuntime.EnsureBridgeInitialized();
        BrowserStateStorageInterop.Write(Kind == StateStorageKind.Session, key, value);
    }

    /// <summary>
    /// Removes a key in one interop crossing; an absent key remains absent without error.
    /// Specified by <c>[STA-11]</c>.
    /// </summary>
    /// <param name="key">The exact storage key.</param>
    /// <exception cref="ArgumentNullException">The key is null.</exception>
    /// <exception cref="InvalidOperationException">The browser bridge is not initialized.</exception>
    /// <exception cref="JSException">Browser policy prevents storage access.</exception>
    public void Remove(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        BrowserRuntime.EnsureBridgeInitialized();
        BrowserStateStorageInterop.Remove(Kind == StateStorageKind.Session, key);
    }
}
