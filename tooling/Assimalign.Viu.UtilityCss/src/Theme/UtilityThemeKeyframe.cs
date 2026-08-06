namespace Assimalign.Viu.UtilityCss;

/// <summary>
/// One immutable animation keyframe owned by a utility theme.
/// </summary>
/// <param name="Name">The CSS keyframe name without an <c>@keyframes</c> prefix.</param>
/// <param name="Body">The normalized contents between the keyframe braces.</param>
public sealed record UtilityThemeKeyframe(
    string Name,
    string Body);
