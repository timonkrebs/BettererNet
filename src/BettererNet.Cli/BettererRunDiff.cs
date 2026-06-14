using System.Text.Json.Nodes;

namespace BettererNet.Cli;

/// <summary>Computes the new file-test issues a run introduced versus its baseline, with file paths.</summary>
internal static class BettererRunDiff
{
    public static IReadOnlyList<(string File, BettererFileIssue Issue)> NewIssues(BettererRunSummary run)
    {
        // Only file tests carry per-issue detail (a JSON object of file -> issues).
        if (run.Result is not JsonObject)
        {
            return [];
        }

        var current = BettererFileIssuesSerializer.Instance.Deserialize(run.Result);
        var baseline = run.Baseline is JsonObject
            ? BettererFileIssuesSerializer.Instance.Deserialize(run.Baseline)
            : new BettererFileIssues();

        var added = new List<(string, BettererFileIssue)>();
        foreach (var (file, issues) in current.Files)
        {
            // Match current issues against the baseline by hash, as a multiset, per file.
            var remaining = new Dictionary<string, int>(StringComparer.Ordinal);
            if (baseline.Files.TryGetValue(file, out var baselineIssues))
            {
                foreach (var issue in baselineIssues)
                {
                    remaining[issue.Hash] = remaining.GetValueOrDefault(issue.Hash) + 1;
                }
            }

            foreach (var issue in issues)
            {
                if (remaining.TryGetValue(issue.Hash, out var count) && count > 0)
                {
                    remaining[issue.Hash] = count - 1;
                }
                else
                {
                    added.Add((file, issue));
                }
            }
        }

        return added;
    }
}
