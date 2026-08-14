using System;

namespace Assimalign.Viu.UtilityCss.LanguageServer;

internal static class TextCoordinateConverter
{
    internal static bool TryGetOffset(
        string text,
        TextPosition position,
        out int offset)
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

    internal static TextPosition GetPosition(string text, int offset)
    {
        var boundedOffset = Math.Clamp(offset, 0, text.Length);
        var line = 0;
        var lineStart = 0;
        for (var index = 0; index < boundedOffset; index++)
        {
            if (text[index] != '\n')
            {
                continue;
            }

            line++;
            lineStart = index + 1;
        }

        return new TextPosition(line, boundedOffset - lineStart);
    }

    internal static TextRange GetRange(string text, int start, int end)
        => new(
            GetPosition(text, start),
            GetPosition(text, end));
}
