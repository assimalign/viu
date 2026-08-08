using System.Threading.Tasks;

namespace Assimalign.Viu;

/// <summary>Represents the next stage of an application-lifetime pipeline.</summary>
/// <param name="context">The application composition and lifetime context.</param>
/// <returns>A task spanning the remaining application lifetime.</returns>
/// <remarks>Specified by <c>[APP-4]</c>.</remarks>
public delegate ValueTask ApplicationDelegate(IApplicationContext context);
