using System;
using System.IO;
using System.Text;

namespace Assimalign.Viu.Sdk.Browser.Tasks;

internal sealed class CssHotReloadWorkerDiagnostics
{
    private const int MaximumTailLength = 8192;
    private readonly object synchronization = new object();
    private string standardOutput = string.Empty;
    private string standardError = string.Empty;

    internal void AppendStandardOutput(string? text)
    {
        if (text is not null)
        {
            lock (synchronization)
            {
                standardOutput = TakeTail(standardOutput + text + Environment.NewLine);
            }
        }
    }

    internal void AppendStandardError(string? text)
    {
        if (text is not null)
        {
            lock (synchronization)
            {
                standardError = TakeTail(standardError + text + Environment.NewLine);
            }
        }
    }

    internal string FormatFailure(string stateFilePath)
    {
        lock (synchronization)
        {
            return "Polled state file: '" + stateFilePath + "'." + Environment.NewLine +
                "Worker stdout tail:" + Environment.NewLine +
                FormatTail(standardOutput, stateFilePath + ".stdout.log") + Environment.NewLine +
                "Worker stderr tail:" + Environment.NewLine +
                FormatTail(standardError, stateFilePath + ".stderr.log");
        }
    }

    private static string FormatTail(string startupOutput, string path)
    {
        var output = TakeTail(startupOutput + ReadFileTail(path));
        return output.Length == 0 ? "(no output captured)" : output;
    }

    private static string ReadFileTail(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return string.Empty;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > MaximumTailLength)
            {
                stream.Seek(-MaximumTailLength, SeekOrigin.End);
            }

            // Bound the read even if a still-running worker appends while diagnostics are collected.
            var bytes = new byte[MaximumTailLength];
            var count = 0;
            int read;
            while (count < bytes.Length &&
                (read = stream.Read(bytes, count, bytes.Length - count)) > 0)
            {
                count += read;
            }

            return Encoding.UTF8.GetString(bytes, 0, count);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return "(could not read '" + path + "': " + exception.Message + ")";
        }
    }

    private static string TakeTail(string text) => text.Length <= MaximumTailLength
        ? text
        : text.Substring(text.Length - MaximumTailLength);
}
