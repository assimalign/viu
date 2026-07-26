using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace Assimalign.Viu.Sdk.CssHotReload;

internal static partial class ProcessLifetime
{
    public static bool TryCapture(
        int processIdentifier,
        out ProcessIdentity identity)
    {
        try
        {
            using var process = Process.GetProcessById(processIdentifier);
            long? startTimeUtcTicks;
            try
            {
                startTimeUtcTicks = process.StartTime.ToUniversalTime().Ticks;
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or NotSupportedException)
            {
                startTimeUtcTicks = null;
            }

            identity = new ProcessIdentity(processIdentifier, startTimeUtcTicks);
            return !process.HasExited;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            identity = default;
            return false;
        }
    }

    public static bool IsAlive(ProcessIdentity identity)
    {
        if (!TryCapture(identity.ProcessIdentifier, out var current))
        {
            return false;
        }

        return identity.StartTimeUtcTicks is null ||
            current.StartTimeUtcTicks is null ||
            identity.StartTimeUtcTicks == current.StartTimeUtcTicks;
    }

    public static bool TryGetParentProcessIdentifier(
        int processIdentifier,
        out int parentProcessIdentifier)
    {
        if (OperatingSystem.IsWindows())
        {
            return TryGetWindowsParentProcessIdentifier(
                processIdentifier,
                out parentProcessIdentifier);
        }

        if (OperatingSystem.IsLinux())
        {
            return TryGetLinuxParentProcessIdentifier(
                processIdentifier,
                out parentProcessIdentifier);
        }

        if (OperatingSystem.IsMacOS())
        {
            return TryGetMacOsParentProcessIdentifier(
                processIdentifier,
                out parentProcessIdentifier);
        }

        parentProcessIdentifier = 0;
        return false;
    }

    private static bool TryGetWindowsParentProcessIdentifier(
        int processIdentifier,
        out int parentProcessIdentifier)
    {
        try
        {
            using var process = Process.GetProcessById(processIdentifier);
            var information = new ProcessBasicInformation();
            var status = NativeMethods.NtQueryInformationProcess(
                process.Handle,
                0,
                ref information,
                Marshal.SizeOf<ProcessBasicInformation>(),
                out _);
            var value = information.InheritedFromUniqueProcessIdentifier.ToInt64();
            if (status != 0 || value <= 0 || value > int.MaxValue)
            {
                parentProcessIdentifier = 0;
                return false;
            }

            parentProcessIdentifier = (int)value;
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
                InvalidOperationException or
                NotSupportedException)
        {
            parentProcessIdentifier = 0;
            return false;
        }
    }

    private static bool TryGetLinuxParentProcessIdentifier(
        int processIdentifier,
        out int parentProcessIdentifier)
    {
        parentProcessIdentifier = 0;
        try
        {
            var status = File.ReadAllText(
                "/proc/" +
                processIdentifier.ToString(CultureInfo.InvariantCulture) +
                "/stat");
            var processNameEnd = status.LastIndexOf(')');
            if (processNameEnd < 0 || processNameEnd + 2 >= status.Length)
            {
                parentProcessIdentifier = 0;
                return false;
            }

            var fields = status.Substring(processNameEnd + 2)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return fields.Length > 1 &&
                int.TryParse(
                    fields[1],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out parentProcessIdentifier) &&
                parentProcessIdentifier > 0;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            parentProcessIdentifier = 0;
            return false;
        }
    }

    private static bool TryGetMacOsParentProcessIdentifier(
        int processIdentifier,
        out int parentProcessIdentifier)
    {
        parentProcessIdentifier = 0;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "/bin/ps",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add("ppid=");
            startInfo.ArgumentList.Add("-p");
            startInfo.ArgumentList.Add(
                processIdentifier.ToString(CultureInfo.InvariantCulture));

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                parentProcessIdentifier = 0;
                return false;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 &&
                int.TryParse(
                    output.Trim(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out parentProcessIdentifier) &&
                parentProcessIdentifier > 0;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
                IOException or
                UnauthorizedAccessException)
        {
            parentProcessIdentifier = 0;
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        public IntPtr Reserved1;
        public IntPtr PebBaseAddress;
        public IntPtr Reserved2First;
        public IntPtr Reserved2Second;
        public IntPtr UniqueProcessIdentifier;
        public IntPtr InheritedFromUniqueProcessIdentifier;
    }

    private static partial class NativeMethods
    {
        [LibraryImport("ntdll.dll")]
        public static partial int NtQueryInformationProcess(
            IntPtr processHandle,
            int processInformationClass,
            ref ProcessBasicInformation processInformation,
            int processInformationLength,
            out int returnLength);
    }
}
