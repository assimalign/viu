using System;
using System.IO;
using System.Text;

namespace Assimalign.Viu.Sdk.CssHotReload;

// [V01.01.12.05.03], #370: the watch-list launcher exits after readiness. Worker-owned files
// retain later diagnostics without depending on the launcher's redirected output pipes.
internal sealed class CssHotReloadDiagnosticOutput : IDisposable
{
    private const string EnvironmentVariable = "VIU_GENERATED_ASSET_HOT_RELOAD_OUTPUT";

    private readonly TextWriter originalStandardOutput;
    private readonly TextWriter originalStandardError;
    private readonly StreamWriter standardOutput;
    private readonly StreamWriter standardError;
    private bool disposed;

    private CssHotReloadDiagnosticOutput(
        StreamWriter standardOutput,
        StreamWriter standardError)
    {
        this.standardOutput = standardOutput;
        this.standardError = standardError;
        originalStandardOutput = Console.Out;
        originalStandardError = Console.Error;
        Console.SetOut(standardOutput);
        Console.SetError(standardError);
    }

    public static CssHotReloadDiagnosticOutput? CreateFromEnvironment()
    {
        var outputPrefix = Environment.GetEnvironmentVariable(EnvironmentVariable);
        Environment.SetEnvironmentVariable(EnvironmentVariable, null);
        if (string.IsNullOrEmpty(outputPrefix))
        {
            // RunHost owns its worker's stdout update protocol and does not request file output.
            return null;
        }

        outputPrefix = Path.GetFullPath(outputPrefix);
        var directory = Path.GetDirectoryName(outputPrefix);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var standardOutput = CreateWriter(outputPrefix + ".stdout.log");
        try
        {
            var standardError = CreateWriter(outputPrefix + ".stderr.log");
            return new CssHotReloadDiagnosticOutput(standardOutput, standardError);
        }
        catch
        {
            standardOutput.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Console.SetOut(originalStandardOutput);
        Console.SetError(originalStandardError);
        try
        {
            standardOutput.Dispose();
        }
        finally
        {
            standardError.Dispose();
        }
    }

    private static StreamWriter CreateWriter(string path) =>
        new StreamWriter(
            new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.ReadWrite | FileShare.Delete),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
        };
}
