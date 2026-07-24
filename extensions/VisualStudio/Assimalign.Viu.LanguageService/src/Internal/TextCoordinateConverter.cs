using System;

namespace Assimalign.Viu.LanguageService;

internal static class TextCoordinateConverter
{
    internal static bool TryGetOffset(string text, LanguagePosition position, out int offset)
    {
        offset = 0;
        if (position.Line < 0 || position.Character < 0)
        {
            return false;
        }

        var currentLine = 0;
        var lineStart = 0;

        while (currentLine < position.Line)
        {
            var newline = text.IndexOf('\n', lineStart);
            if (newline < 0)
            {
                return false;
            }

            lineStart = newline + 1;
            currentLine++;
        }

        var lineEnd = text.IndexOf('\n', lineStart);
        if (lineEnd < 0)
        {
            lineEnd = text.Length;
        }

        if (lineEnd > lineStart && text[lineEnd - 1] == '\r')
        {
            lineEnd--;
        }

        if (position.Character > lineEnd - lineStart)
        {
            return false;
        }

        offset = lineStart + position.Character;
        return true;
    }

    internal static LanguagePosition GetPosition(string text, int offset)
    {
        offset = Math.Clamp(offset, 0, text.Length);
        var line = 0;
        var lineStart = 0;

        for (var index = 0; index < offset; index++)
        {
            if (text[index] != '\n')
            {
                continue;
            }

            line++;
            lineStart = index + 1;
        }

        return new LanguagePosition(line, offset - lineStart);
    }

    internal static string GetLinePrefix(string text, int offset)
    {
        var lineStart = offset;
        while (lineStart > 0 && text[lineStart - 1] != '\n')
        {
            lineStart--;
        }

        return text.Substring(lineStart, offset - lineStart);
    }
}
