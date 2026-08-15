using System.Threading.Tasks;

namespace Assimalign.Viu.Sdk.Browser.RunHost;

internal static class Program
{
    internal static async Task<int> Main(string[] arguments) =>
        await RunHostProcess.RunAsync(
            arguments,
            System.Console.Out,
            System.Console.Error);
}
