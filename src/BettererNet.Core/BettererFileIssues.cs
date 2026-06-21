namespace BettererNet;

/// <summary>The set of issues a <see cref="BettererFileTest"/> reports, grouped by file.</summary>
public sealed class BettererFileIssues
{
    private readonly SortedDictionary<string, List<BettererFileIssue>> _files = new(StringComparer.Ordinal);

    /// <summary>Issues keyed by file path (ordinal-sorted).</summary>
    public IReadOnlyDictionary<string, List<BettererFileIssue>> Files => _files;

    /// <summary>The total number of issues across all files - the metric the constraint compares.</summary>
    public int TotalCount => _files.Values.Sum(issues => issues.Count);

    /// <summary>Record an issue. The issue's hash is derived from its message, column, and length.</summary>
    public BettererFileIssues Add(string file, int line, int column, int length, string message)
    {
        var hash = BettererHash.Compute($"{message}|{column}|{length}");
        GetIssues(file).Add(new BettererFileIssue
        {
            Line = line,
            Column = column,
            Length = length,
            Message = message,
            Hash = hash,
        });
        return this;
    }

    internal void AddExisting(string file, BettererFileIssue issue) => GetIssues(file).Add(issue);

    private List<BettererFileIssue> GetIssues(string file)
    {
        if (!_files.TryGetValue(file, out var issues))
        {
            issues = new List<BettererFileIssue>();
            _files[file] = issues;
        }

        return issues;
    }

    /// <summary>
    /// Compare two results by issue hash (per file, as a multiset) to report which issues are new
    /// and which were fixed - independent of the lines they appear on.
    /// </summary>
    public static BettererFileIssuesDiff Diff(BettererFileIssues baseline, BettererFileIssues current)
    {
        var added = new List<BettererFileIssue>();
        var fixedIssues = new List<BettererFileIssue>();

        foreach (var file in baseline.Files.Keys.Union(current.Files.Keys, StringComparer.Ordinal))
        {
            var baselineIssues = baseline.Files.TryGetValue(file, out var b) ? (IReadOnlyList<BettererFileIssue>)b : [];
            var currentIssues = current.Files.TryGetValue(file, out var c) ? (IReadOnlyList<BettererFileIssue>)c : [];

            added.AddRange(Unmatched(currentIssues, baselineIssues));
            fixedIssues.AddRange(Unmatched(baselineIssues, currentIssues));
        }

        return new BettererFileIssuesDiff { New = added, Fixed = fixedIssues };
    }

    // Issues in `source` whose hashes are not covered (as a multiset) by `other`.
    private static IEnumerable<BettererFileIssue> Unmatched(
        IReadOnlyList<BettererFileIssue> source,
        IReadOnlyList<BettererFileIssue> other)
    {
        var available = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var issue in other)
        {
            available[issue.Hash] = available.GetValueOrDefault(issue.Hash) + 1;
        }

        foreach (var issue in source)
        {
            if (available.TryGetValue(issue.Hash, out var count) && count > 0)
            {
                available[issue.Hash] = count - 1;
            }
            else
            {
                yield return issue;
            }
        }
    }
}

/// <summary>The difference between two <see cref="BettererFileIssues"/> results.</summary>
public sealed class BettererFileIssuesDiff
{
    public required IReadOnlyList<BettererFileIssue> New { get; init; }

    public required IReadOnlyList<BettererFileIssue> Fixed { get; init; }

    public bool IsUnchanged => New.Count == 0 && Fixed.Count == 0;
}
