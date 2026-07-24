using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;
using Microsoft.VisualStudio.Extensibility.LanguageServer;
using Microsoft.VisualStudio.RpcContracts.LanguageServerProvider;

namespace Assimalign.Viu.VisualStudio;

#pragma warning disable VSEXTPREVIEW_LSP

/// <summary>
/// Connects Visual Studio to the standalone Viu language server over standard input and output.
/// </summary>
[VisualStudioContribution]
internal sealed class ViuLanguageServerProvider : LanguageServerProvider
{
    /// <summary>
    /// Gets the document type contributed for <c>.viu</c> single-file components.
    /// </summary>
    [VisualStudioContribution]
    public static DocumentTypeConfiguration ViuDocumentType => new("viu")
    {
        FileExtensions = [".viu"],
        BaseDocumentType = LanguageServerBaseDocumentType,
    };

    /// <inheritdoc />
    public override LanguageServerProviderConfiguration LanguageServerProviderConfiguration => new(
        "%Assimalign.Viu.VisualStudio.LanguageServer.DisplayName%",
        [DocumentFilter.FromDocumentType(ViuDocumentType)]);

    /// <inheritdoc />
    public override Task<IDuplexPipe?> CreateServerConnectionAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string assemblyLocation = Assembly.GetExecutingAssembly().Location;
        string extensionDirectory =
            Path.GetDirectoryName(assemblyLocation) ?? Directory.GetCurrentDirectory();
        ViuLanguageServerConfiguration configuration =
            ViuLanguageServerConfiguration.Load(extensionDirectory);
        string executablePath = configuration.ResolveExecutablePath(
            extensionDirectory,
            RuntimeInformation.ProcessArchitecture);

        if (!File.Exists(executablePath))
        {
            this.Enabled = false;
            return Task.FromResult<IDuplexPipe?>(null);
        }

        ProcessStartInfo startInformation = new()
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? extensionDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (string argument in configuration.Arguments)
        {
            startInformation.ArgumentList.Add(argument);
        }

#pragma warning disable CA2000
        Process process = new()
        {
            StartInfo = startInformation,
        };
#pragma warning restore CA2000

        if (!process.Start())
        {
            process.Dispose();
            this.Enabled = false;
            return Task.FromResult<IDuplexPipe?>(null);
        }

        return Task.FromResult<IDuplexPipe?>(
            new DuplexPipe(
                PipeReader.Create(process.StandardOutput.BaseStream),
                PipeWriter.Create(process.StandardInput.BaseStream)));
    }

    /// <inheritdoc />
    public override Task OnServerInitializationResultAsync(
        ServerInitializationResult serverInitializationResult,
        LanguageServerInitializationFailureInfo? initializationFailureInformation,
        CancellationToken cancellationToken)
    {
        if (serverInitializationResult == ServerInitializationResult.Failed)
        {
            this.Enabled = false;
        }

        return base.OnServerInitializationResultAsync(
            serverInitializationResult,
            initializationFailureInformation,
            cancellationToken);
    }
}

#pragma warning restore VSEXTPREVIEW_LSP
