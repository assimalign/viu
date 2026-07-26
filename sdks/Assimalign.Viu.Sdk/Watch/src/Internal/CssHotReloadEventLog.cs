using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Assimalign.Viu.Sdk.CssHotReload;

internal sealed class CssHotReloadEventLog
{
    private const int AppendAttemptCount = 20;
    private const int AppendRetryDelayMilliseconds = 10;

    private readonly string path;
    private readonly object synchronization = new object();

    public CssHotReloadEventLog(string path)
    {
        this.path = path;
    }

    public void Append(string value)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        lock (synchronization)
        {
            for (var attempt = 0; attempt < AppendAttemptCount; attempt++)
            {
                try
                {
                    var directory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.AppendAllText(
                        path,
                        value + Environment.NewLine,
                        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    return;
                }
                catch (IOException) when (attempt + 1 < AppendAttemptCount)
                {
                    Thread.Sleep(AppendRetryDelayMilliseconds);
                }
                catch (IOException)
                {
                    // Diagnostics must not stop regeneration when a reader retains the log.
                    return;
                }
            }
        }
    }
}
