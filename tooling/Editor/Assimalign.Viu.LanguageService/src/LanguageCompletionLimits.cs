namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Bounds applied to completion results before they cross the language-server protocol boundary.
/// </summary>
public static class LanguageCompletionLimits
{
    /// <summary>
    /// The maximum number of completion items returned for one request.
    /// </summary>
    /// <remarks>
    /// A truncated result is reported to the client as an incomplete list, which is the
    /// language-server protocol contract for re-requesting as the user keeps typing.
    /// </remarks>
    public const int MaximumItems = 500;
}
