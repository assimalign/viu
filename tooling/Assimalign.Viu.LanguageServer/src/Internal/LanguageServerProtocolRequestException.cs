using System;

namespace Assimalign.Viu.LanguageServer;

internal sealed class LanguageServerProtocolRequestException : Exception
{
    internal LanguageServerProtocolRequestException(int code, string message)
        : base(message)
        => Code = code;

    internal int Code { get; }
}
