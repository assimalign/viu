using System;

namespace Assimalign.Viu.Router;

/// <summary>
/// Compares two compiled path patterns by their specificity score. The C# port of vue-router's
/// <c>compareScoreArray</c> and <c>comparePathParserScore</c>
/// (<c>packages/router/src/matcher/pathParserRanker.ts</c>). A negative result means the first
/// pattern is more specific (should sort earlier); positive means less specific; zero means equal.
/// </summary>
/// <remarks>
/// This is what makes ranking table-order-independent: static segments outrank dynamic ones,
/// dynamic outrank the catch-all wildcard, and a longer, more-constrained pattern outranks a
/// shorter or looser one regardless of the order routes were added.
/// </remarks>
internal static class PathParserScoreComparer
{
    /// <summary>
    /// Compares two full scores (one inner array per path segment). Mirrors
    /// <c>comparePathParserScore</c>.
    /// </summary>
    public static double CompareScore(double[][] left, double[][] right)
    {
        var index = 0;
        while (index < left.Length && index < right.Length)
        {
            var comparison = CompareScoreArray(left[index], right[index]);
            if (comparison != 0)
            {
                return comparison;
            }
            index++;
        }

        if (Math.Abs(right.Length - left.Length) == 1)
        {
            if (IsLastScoreNegative(left))
            {
                return 1;
            }
            if (IsLastScoreNegative(right))
            {
                return -1;
            }
        }

        // When every shared segment ties, the pattern with more segments sorts first.
        return right.Length - left.Length;
    }

    // Mirrors compareScoreArray: element-wise until they differ, then a length tie-break that keeps
    // a single static segment sorted ahead of a longer sub-segmented one.
    private static double CompareScoreArray(double[] left, double[] right)
    {
        var index = 0;
        while (index < left.Length && index < right.Length)
        {
            var difference = right[index] - left[index];
            if (difference != 0)
            {
                return difference;
            }
            index++;
        }

        if (left.Length < right.Length)
        {
            return left.Length == 1 && left[0] == PathScore.Static + PathScore.Segment ? -1 : 1;
        }
        if (left.Length > right.Length)
        {
            return right.Length == 1 && right[0] == PathScore.Static + PathScore.Segment ? 1 : -1;
        }
        return 0;
    }

    // Mirrors isLastScoreNegative: whether the final sub-segment of the final segment is a penalty
    // (a wildcard/optional/repeatable), used to order a catch-all below its more-specific siblings.
    private static bool IsLastScoreNegative(double[][] score)
    {
        if (score.Length == 0)
        {
            return false;
        }
        var last = score[^1];
        return last.Length > 0 && last[^1] < 0;
    }
}
