using System.IO;
using System.Text;
using System.Threading;

namespace Assimalign.Viu.Compiler.SingleFileComponent;

internal static class ProjectionPipeline
{
    internal static SingleFileComponentProjectionResult Project(
        SingleFileComponentProjectionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fileName = Path.GetFileNameWithoutExtension(request.FilePath);
        var className = ToIdentifier(fileName);
        var componentNamespace = request.RootNamespace;
        var source = string.Concat(
            "// Contract-model projection for ",
            request.FilePath,
            "\n");

        return new SingleFileComponentProjectionResult(
            string.Concat(className, ".g.cs"),
            source,
            className,
            componentNamespace);
    }

    private static string ToIdentifier(string value)
    {
        var builder = new StringBuilder(value.Length + 1);
        if (value.Length == 0 || !IsIdentifierStart(value[0]))
        {
            builder.Append('_');
        }

        foreach (var character in value)
        {
            builder.Append(IsIdentifierPart(character) ? character : '_');
        }

        return builder.ToString();
    }

    private static bool IsIdentifierStart(char value) =>
        value == '_' || char.IsLetter(value);

    private static bool IsIdentifierPart(char value) =>
        value == '_' || char.IsLetterOrDigit(value);
}
