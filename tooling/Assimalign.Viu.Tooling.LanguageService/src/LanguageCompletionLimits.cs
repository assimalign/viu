namespace Assimalign.Viu.Tooling.LanguageService;

/// <summary>
/// Bounds applied to completion results before they cross the language-server protocol boundary.
/// </summary>
public static class LanguageCompletionLimits
{
    /// <summary>
    /// The maximum number of completion items returned for one request.
    /// </summary>
    /// <remarks>
    /// The utility registry expands to well over a hundred thousand themed candidates, so an
    /// unfiltered class position would otherwise serialize a payload no editor can consume. A
    /// truncated result is reported to the client as an incomplete list, which is the language-server
    /// protocol contract for "re-request as the user keeps typing" and is how the shared registry
    /// narrows to an exact candidate.
    /// </remarks>
    public const int MaximumItems = 500;
}
