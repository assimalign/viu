using System;
using System.Threading.Tasks;

namespace Assimalign.Viu.Testing.EndToEnd;

internal static class Program
{
    internal static async Task<int> Main(string[] arguments)
    {
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
}
