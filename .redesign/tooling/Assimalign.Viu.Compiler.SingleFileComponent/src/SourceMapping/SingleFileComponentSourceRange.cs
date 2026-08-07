using System;

namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>Identifies one zero-based source range without depending on Roslyn editor types.</summary>
public sealed class SingleFileComponentSourceRange
{
    /// <summary>Initializes a validated source range.</summary>
    /// <param name="start">The zero-based start offset.</param>
    /// <param name="length">The non-negative length.</param>
    public SingleFileComponentSourceRange(int start, int length)
    {
        if (start < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        Start = start;
        Length = length;
    }

    /// <summary>Gets the zero-based start offset.</summary>
    public int Start { get; }

    /// <summary>Gets the non-negative length.</summary>
    public int Length { get; }
}
