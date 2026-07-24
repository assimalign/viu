using System;
using System.Threading.Tasks;

namespace Assimalign.Viu.LanguageServer;

internal static class Program
{
    public static async Task<int> Main()
    {
        await using var input = Console.OpenStandardInput();
        await using var output = Console.OpenStandardOutput();
        var host = new LanguageServerHost();
        return await host.RunAsync(input, output).ConfigureAwait(false);
    }
}
