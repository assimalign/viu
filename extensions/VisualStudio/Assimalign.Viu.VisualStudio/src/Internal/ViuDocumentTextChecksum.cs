using System;
using System.Security.Cryptography;
using System.Text;

namespace Assimalign.Viu.VisualStudio;

/// <summary>Computes the server-compatible identity of one editor text snapshot.</summary>
internal static class ViuDocumentTextChecksum
{
    /// <summary>Computes a base-64 SHA-256 checksum over the text's UTF-8 representation.</summary>
    internal static string Compute(string text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        using (var algorithm = SHA256.Create())
        {
            return Convert.ToBase64String(algorithm.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }
    }
}
