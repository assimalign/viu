namespace Assimalign.Viu.UtilityCss;

internal readonly struct TextSegment
{
    public TextSegment(string text, int start)
    {
        Text = text;
        Start = start;
    }

    public string Text { get; }

    public int Start { get; }
}
