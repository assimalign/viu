using System.Threading.Tasks;

namespace Assimalign.Viu;

/// <summary>Represents behavior surrounding a complete persistent application lifetime.</summary>
/// <param name="context">The application composition and lifetime context.</param>
/// <param name="next">The next pipeline stage.</param>
/// <returns>A task spanning this middleware's contribution to the lifetime.</returns>
/// <remarks>
/// Registrations retain order and are never deduplicated. Specified by <c>[APP-3]</c> and
/// <c>[APP-4]</c>.
/// </remarks>
public delegate ValueTask ApplicationMiddleware(
    IApplicationContext context,
    ApplicationDelegate next);
