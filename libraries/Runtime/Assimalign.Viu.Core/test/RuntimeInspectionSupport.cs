using System;
using System.Runtime.CompilerServices;

namespace Assimalign.Viu.Core.Tests;

/// <summary>
/// Enables runtime inspection support before any test touches the seam. The feature switch is
/// read once per process when the inspection types initialize [DVT-12], so a per-test toggle
/// would be too late; the switch-disabled path is the trimmer's constant substitution.
/// </summary>
internal static class RuntimeInspectionSupport
{
    [ModuleInitializer]
    internal static void Enable() =>
        AppContext.SetSwitch(RuntimeInspection.FeatureSwitchName, true);
}
