using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Assimalign.Viu.Generators.FileRouting;

internal static class FileRoutingDiagnostics
{
    private const string Category = "Assimalign.Viu.Generators.FileRouting";

    internal static readonly DiagnosticDescriptor DuplicateComponent = new("VIU2001", "Duplicate page component identity",
        "Page component identity '{0}' is also produced by '{1}'; component factory registration would throw, so rename one page file", Category, DiagnosticSeverity.Error, true);
    internal static readonly DiagnosticDescriptor DuplicatePath = new("VIU2002", "Duplicate route path",
        "Route path '{0}' is also produced by '{1}'", Category, DiagnosticSeverity.Error, true);
    internal static readonly DiagnosticDescriptor DuplicateName = new("VIU2003", "Duplicate route name",
        "Route name '{0}' is also produced by '{1}'; choose a unique route name override or rename the page", Category, DiagnosticSeverity.Error, true);
    internal static readonly DiagnosticDescriptor CatchAllPosition = new("VIU2004", "Catch-all must be last",
        "Catch-all parameter in '{0}' must be the last route segment", Category, DiagnosticSeverity.Error, true);
    internal static readonly DiagnosticDescriptor InvalidSegment = new("VIU2005", "Invalid page segment",
        "Page segment '{0}' is invalid; use ASCII words separated by single hyphens or underscores, or [Name], [[Name]], [...Name]", Category, DiagnosticSeverity.Error, true);
    internal static readonly DiagnosticDescriptor MalformedRoute = new("VIU2006", "Malformed route block",
        "Route block is malformed: {0}", Category, DiagnosticSeverity.Error, true);
    internal static readonly DiagnosticDescriptor InvalidConfiguration = new("VIU2007", "Invalid file routing configuration",
        "File routing requires ProjectDir and a nonempty ViuFileRoutingPagesDirectory relative to it", Category, DiagnosticSeverity.Error, true);

    internal static Diagnostic Create(DiagnosticDescriptor descriptor, FileRoutingInput input, params object[] arguments)
        => Diagnostic.Create(descriptor, Location.Create(input.Path, new TextSpan(0, 0), new LinePositionSpan(new LinePosition(0, 0), new LinePosition(0, 0))), arguments);
}
