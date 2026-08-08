using System;
using System.Threading;

namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// Provides the stable operation-oriented facade shared by generators and editor services.
/// </summary>
/// <remarks>Specified by <c>[SFC-PIPE-2]</c> and <c>[TOOL-2]</c>.</remarks>
public static class SingleFileComponentCompiler
{
    /// <summary>Projects one source container through the shared internal pipeline.</summary>
    /// <param name="request">Editor-neutral source and options.</param>
    /// <param name="cancellationToken">Cancellation for parsing, analysis, and emission.</param>
    /// <returns>The generated source, diagnostics, identities, and source mappings.</returns>
    public static SingleFileComponentProjectionResult Project(
        SingleFileComponentProjectionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return ProjectionPipeline.Project(request, cancellationToken);
    }
}
