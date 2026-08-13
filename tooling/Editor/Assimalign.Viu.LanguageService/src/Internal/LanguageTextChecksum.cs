using System;
using System.Security.Cryptography;
using System.Text;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Computes the stable text identity carried beside asynchronous semantic classifications.
/// </summary>
internal static class LanguageTextChecksum
{
    /// <summary>Computes a base-64 SHA-256 checksum over the text's UTF-8 representation.</summary>
    internal static string Compute(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        using var algorithm = SHA256.Create();
        return Convert.ToBase64String(algorithm.ComputeHash(Encoding.UTF8.GetBytes(text)));
    }
}
