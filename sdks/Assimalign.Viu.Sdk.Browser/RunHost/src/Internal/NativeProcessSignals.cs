using System.Runtime.InteropServices;

namespace Assimalign.Viu.Sdk.Browser.RunHost;

internal static partial class NativeProcessSignals
{
    internal const int Termination = 15;

    [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
    internal static partial int Send(int processIdentifier, int signal);
}
