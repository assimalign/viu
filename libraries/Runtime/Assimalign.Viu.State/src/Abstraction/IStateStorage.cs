namespace Assimalign.Viu.State;

/// <summary>
/// Stores complete string payloads behind a host-owned synchronous boundary. Implementations may
/// throw on unavailable storage; the persistence plugin contains those failures and reports them.
/// Specified by <c>[STA-11]</c>.
/// </summary>
public interface IStateStorage
{
    /// <summary>
    /// Reads one complete payload. Returns false and an empty value when the key is absent.
    /// Specified by <c>[STA-11]</c>.
    /// </summary>
    /// <param name="key">The storage key, compared ordinally.</param>
    /// <param name="value">The stored payload, or an empty string when absent.</param>
    /// <returns>Whether the key exists, including when its stored value is empty.</returns>
    bool TryRead(string key, out string value);

    /// <summary>
    /// Replaces one key's complete payload in a single storage operation. A failure may throw and
    /// must not partially replace the value. Specified by <c>[STA-11]</c>.
    /// </summary>
    /// <param name="key">The storage key.</param>
    /// <param name="value">The complete string payload.</param>
    void Write(string key, string value);

    /// <summary>Removes a key, doing nothing when absent. Specified by <c>[STA-11]</c>.</summary>
    /// <param name="key">The storage key.</param>
    void Remove(string key);
}
