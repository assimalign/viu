namespace Assimalign.Viu.UtilityCss;

internal readonly struct UtilityCandidateScanRegion
{
    public UtilityCandidateScanRegion(
        int start,
        int end,
        bool rejectsStartBoundary,
        bool rejectsEndBoundary)
    {
        Start = start;
        End = end;
        RejectsStartBoundary = rejectsStartBoundary;
        RejectsEndBoundary = rejectsEndBoundary;
    }

    public int Start { get; }

    public int End { get; }

    public bool RejectsStartBoundary { get; }

    public bool RejectsEndBoundary { get; }
}
