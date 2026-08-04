namespace Assimalign.Viu.Tooling.UtilityCss;

internal readonly struct UtilityCandidateTextSpan
{
    public UtilityCandidateTextSpan(int start, int length)
    {
        Start = start;
        Length = length;
    }

    public int Start { get; }

    public int Length { get; }

    public int End => Start + Length;
}
