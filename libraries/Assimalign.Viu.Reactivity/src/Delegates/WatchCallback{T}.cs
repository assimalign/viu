namespace Assimalign.Viu.Reactivity;

/// <summary>
/// A <c>watch</c> callback, receiving <c>(value, oldValue, onCleanup)</c>. Invoked
/// after the watched source changes with the new and previous values; on the very first call of an
/// <c>Immediate</c> watcher <paramref name="oldValue"/> is <see langword="default"/> (unset).
/// </summary>
/// <typeparam name="T">The watched value type (an object array for the multi-source form).</typeparam>
/// <param name="value">The new source value.</param>
/// <param name="oldValue">The previous source value; <see langword="default"/> on the immediate first run.</param>
/// <param name="onCleanup">
/// Registers a side-effect cleanup that runs immediately before the next callback and again when the
/// watcher stops, allowing stale asynchronous work to be cancelled before the next value is
/// observed. Specified by <c>[RCT-5]</c>.
/// </param>
public delegate void WatchCallback<T>(T value, T oldValue, OnCleanup onCleanup);

