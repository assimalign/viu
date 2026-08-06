using System.Threading.Tasks;

namespace Assimalign.Viu;

/// <summary>Represents the next stage of an application lifetime pipeline.</summary>
/// <param name="context">The current application execution.</param>
/// <returns>A task that spans the remaining application lifetime.</returns>
/// <remarks>Specified by <c>[APP-4]</c>.</remarks>
public delegate ValueTask ApplicationDelegate(ApplicationExecutionContext context);
