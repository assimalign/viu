using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>Runs an explicitly configured static generator from an application's console entry point.</summary>
/// <remarks>Parses only declared options and loads no assemblies. The host returns zero on success and one on failure or cancellation. Specified by <c>[SSG-6]</c>.</remarks>
public static class StaticSiteGeneratorHost
{
    /// <summary>Parses prerender arguments, reads the published host page, generates files, and logs each emitted location.</summary>
    /// <param name="arguments">Optional leading prerender, then --output, repeatable --route or --routes files, --host-page, optional --base and --mount.</param>
    /// <param name="configure">Creates a generator through the application's explicit registrations.</param>
    /// <param name="cancellationToken">Cancellation propagated through input reads and generation.</param>
    /// <returns>Zero after every file is emitted; one after diagnostics are written to standard error.</returns>
    /// <remarks>Route files are UTF-8, one route per nonblank line. An omitted route list defaults to root. The shell never rewrites base tags or asset references. Specified by <c>[SSG-3]</c> and <c>[SSG-6]</c>.</remarks>
    public static async Task<int> RunAsync(
        string[] arguments,
        Func<StaticSiteGenerator> configure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(configure);
        try
        {
            string? output = null;
            string? hostPage = null;
            string basePath = "/";
            string mount = "#app";
            List<string> routes = [];
            HashSet<string> singularOptions = new(StringComparer.Ordinal);
            for (int index = arguments.Length > 0 && arguments[0] == "prerender" ? 1 : 0; index < arguments.Length; index++)
            {
                string option = arguments[index];
                if (option is not ("--output" or "--host-page" or "--base" or "--mount" or "--route" or "--routes"))
                {
                    throw new ArgumentException($"Unknown static prerender option '{option}'.");
                }

                if (++index >= arguments.Length || string.IsNullOrWhiteSpace(arguments[index]) || arguments[index].StartsWith("--", StringComparison.Ordinal))
                {
                    throw new ArgumentException($"Option '{option}' requires a value.");
                }

                if (option is not ("--route" or "--routes") && !singularOptions.Add(option))
                {
                    throw new ArgumentException($"Option '{option}' was supplied more than once.");
                }

                string value = arguments[index];
                switch (option)
                {
                    case "--output": output = value; break;
                    case "--host-page": hostPage = value; break;
                    case "--base": basePath = value; break;
                    case "--mount": mount = value; break;
                    case "--route": routes.Add(value); break;
                    case "--routes":
                        foreach (string line in await File.ReadAllLinesAsync(value, cancellationToken).ConfigureAwait(false))
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                routes.Add(line.Trim());
                            }
                        }

                        break;
                }
            }

            if (output is null || hostPage is null)
            {
                throw new ArgumentException("Static prerender requires --output <directory> and --host-page <published index.html>.");
            }

            if (routes.Count == 0)
            {
                routes.Add("/");
            }

            string html = await File.ReadAllTextAsync(hostPage, cancellationToken).ConfigureAwait(false);
            IServerRenderDocumentShell shell = new HostPageDocumentShell(html, mount);
            await configure().GenerateAsync(routes, shell, new FileSystemStaticSiteOutput(output), basePath,
                file => Console.Out.WriteLine($"Prerendered '{file.Route}' -> {file.Location}"), cancellationToken).ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception)
        {
            await Console.Error.WriteLineAsync(exception is OperationCanceledException
                ? "Static prerender cancelled."
                : exception.Message).ConfigureAwait(false);
            return 1;
        }
    }
}
