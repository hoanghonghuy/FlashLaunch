using System;

namespace FlashLaunch.Core.Utilities;

public static class FuzzyMatcher
{
    public static double CalculateScore(string source, string query)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(query))
        {
            return 0;
        }

        source = source.Trim();
        query = query.Trim();

        if (source.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return 1.0;
        }

        if (source.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 0.9;
        }

        if (source.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return 0.75;
        }

        var lcs = LongestCommonSubsequence(source.ToLowerInvariant(), query.ToLowerInvariant());
        return Math.Min(0.7 * lcs / source.Length, 0.7);
    }

    private static int LongestCommonSubsequence(string a, string b)
    {
        var dp = new int[a.Length + 1, b.Length + 1];
        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                if (a[i - 1] == b[j - 1])
                {
                    dp[i, j] = dp[i - 1, j - 1] + 1;
                }
                else
                {
                    dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
                }
            }
        }

        return dp[a.Length, b.Length];
    }
}
