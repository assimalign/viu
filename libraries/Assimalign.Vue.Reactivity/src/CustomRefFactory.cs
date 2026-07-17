namespace Assimalign.Vue.Reactivity;

/// <summary>
/// Factory delegate for <see cref="CustomRef{T}"/>: receives the <paramref name="track"/> and
/// <paramref name="trigger"/> delegates wired to the ref's dep and returns the getter/setter pair
/// implementing the ref — the counterpart of Vue's <c>customRef()</c> factory.
/// </summary>
/// <typeparam name="T">The type of the contained value.</typeparam>
/// <param name="track">Call inside the getter to register the ambient subscriber.</param>
/// <param name="trigger">Call to notify subscribers that the value changed.</param>
/// <returns>The getter and setter used by <see cref="CustomRef{T}.Value"/>.</returns>
public delegate (Func<T> Get, Action<T> Set) CustomRefFactory<T>(Action track, Action trigger);
