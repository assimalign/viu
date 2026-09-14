using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace Assimalign.Viu.Sdk.Browser.Tasks;

internal static class CssHotReloadWorkerState
{
    internal static bool TryReadLiveWorker(string stateFilePath, out int processIdentifier)
    {
        processIdentifier = 0;
        try
        {
            if (!File.Exists(stateFilePath))
            {
                return false;
            }

            var recordedProcessIdentifier = 0;
            var processStartTicks = 0L;
            var processStartClockTicks = 0L;
            foreach (var line in File.ReadAllLines(stateFilePath))
            {
                if (line.StartsWith("worker=", StringComparison.Ordinal))
                {
                    int.TryParse(line.Substring("worker=".Length), NumberStyles.None,
                        CultureInfo.InvariantCulture, out recordedProcessIdentifier);
                }
                else if (line.StartsWith("worker-start=", StringComparison.Ordinal))
                {
                    long.TryParse(line.Substring("worker-start=".Length), NumberStyles.None,
                        CultureInfo.InvariantCulture, out processStartTicks);
                }
                else if (line.StartsWith("worker-start-clock-ticks=", StringComparison.Ordinal))
                {
                    long.TryParse(line.Substring("worker-start-clock-ticks=".Length), NumberStyles.None,
                        CultureInfo.InvariantCulture, out processStartClockTicks);
                }
            }

            if (recordedProcessIdentifier <= 0)
            {
                return false;
            }

            using var process = Process.GetProcessById(recordedProcessIdentifier);
            if (process.HasExited)
            {
                return false;
            }

            bool matches;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // [V01.01.12.05.03], #370: Linux Process.StartTime adds a per-process estimate
                // of wall-clock boot time. Compare the kernel's unchanged start token instead;
                // a tolerance on wall-clock ticks would weaken protection against reused identifiers.
                var status = File.ReadAllText("/proc/" +
                    recordedProcessIdentifier.ToString(CultureInfo.InvariantCulture) + "/stat");
                matches = processStartClockTicks > 0 &&
                    TryParseLinuxStartClockTicks(status, out var currentStartClockTicks) &&
                    currentStartClockTicks == processStartClockTicks;
            }
            else
            {
                matches = processStartTicks > 0 &&
                    process.StartTime.ToUniversalTime().Ticks == processStartTicks;
            }

            if (!matches || process.HasExited)
            {
                return false;
            }

            processIdentifier = recordedProcessIdentifier;
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or IOException or
                UnauthorizedAccessException or Win32Exception or NotSupportedException)
        {
            return false;
        }
    }

    internal static bool TryParseLinuxStartClockTicks(string status, out long startClockTicks)
    {
        startClockTicks = 0;
        var processNameEnd = status.LastIndexOf(')');
        if (processNameEnd < 0 || processNameEnd + 2 >= status.Length)
        {
            return false;
        }

        // The parenthesized process name may itself contain spaces and closing parentheses.
        // Fields following its final ')' start at field 3; starttime is field 22.
        var fields = status.Substring(processNameEnd + 2)
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return fields.Length > 19 &&
            long.TryParse(fields[19], NumberStyles.None, CultureInfo.InvariantCulture, out startClockTicks) &&
            startClockTicks > 0;
    }
}
