using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.HackerNews;

/// <summary>
/// The reactive state of <see cref="UserStore"/> — one user profile, with explicit loading/error.
/// </summary>
[Reactive]
internal partial class UserState
{
    /// <summary>The user id currently being shown.</summary>
    public partial string? ActiveId { get; set; }

    /// <summary>The loaded profile, or null while loading or when missing.</summary>
    public partial HackerNewsUser? Profile { get; set; }

    /// <summary>Whether a load is in flight.</summary>
    public partial bool IsLoading { get; set; }

    /// <summary>The last load error message, or null (includes the "not found" case).</summary>
    public partial string? Error { get; set; }
}
