using System;
using System.Threading.Tasks;

namespace Assimalign.Viu.Testing.EndToEnd;

internal static class Program
{
    internal static async Task<int> Main(string[] arguments)
    {
        if (IsWatchBrowserLaunchSink(arguments))
        {
            // [V01.01.12.31], #349: Keep the launch process alive long enough for dotnet-watch to
            // record a successful first launch. Playwright owns the connected scenario browser.
            await Task.Delay(TimeSpan.FromSeconds(1));
            return 0;
        }

        try
        {
            HarnessOptions options = HarnessOptions.Parse(arguments);
            return await new EndToEndHarness(options).RunAsync();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 2;
        }
    }

    private static bool IsWatchBrowserLaunchSink(string[] arguments) =>
        string.Equals(
            Environment.GetEnvironmentVariable(
                "VIU_END_TO_END_BROWSER_LAUNCH_SINK"),
            "1",
            StringComparison.Ordinal)
        && arguments.Length == 1
        && Uri.TryCreate(arguments[0], UriKind.Absolute, out Uri? address)
        && (address.Scheme == Uri.UriSchemeHttp
            || address.Scheme == Uri.UriSchemeHttps);
}
