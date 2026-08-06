using System.Threading.Tasks;

namespace Assimalign.Viu;

/// <summary>Represents behavior that surrounds a complete application lifetime.</summary>
/// <param name="context">The immutable composition and runtime state for the current application.</param>
/// <param name="next">The next stage of the application pipeline.</param>
/// <returns>A task that spans this middleware's contribution to the application lifetime.</returns>
/// <remarks>Registrations retain order and are never deduplicated. Specified by <c>[APP-3]</c>.</remarks>
public delegate ValueTask ApplicationMiddleware(
    IApplicationContext context,
    ApplicationDelegate next);
