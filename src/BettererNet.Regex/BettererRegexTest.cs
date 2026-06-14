using System.Text.RegularExpressions;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace BettererNet;

/// <summary>
/// Factory for regex tests: count matches of a pattern across globbed files. The .NET analog of
/// betterer's <c>regexp</c> test — handy for banning APIs or burning down TODOs.
/// </summary>
public static class BettererRegexTest
{
    // Fixed, safe pattern — compiled once and reused rather than recompiled per match.
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.None, TimeSpan.FromSeconds(1));

    /// <summary>
    /// Create a regex test. Each match becomes a file issue, so the test fails when new matches
    /// appear and ratchets down as they are removed.
    /// </summary>
    /// <param name="includes">Glob patterns (e.g. <c>"**/*.cs"</c>) relative to <paramref name="baseDirectory"/>.</param>
    /// <param name="baseDirectory">Root to glob from. Defaults to the current directory.</param>
    /// <param name="matchTimeout">Per-match timeout guarding against ReDoS. Defaults to 2 seconds.</param>
    public static BettererTest<BettererFileIssues> Create(
        string name,
        string pattern,
        IReadOnlyList<string> includes,
        string? baseDirectory = null,
        RegexOptions options = RegexOptions.None,
        TimeSpan? matchTimeout = null,
        Func<BettererFileIssues, bool>? goal = null,
        DateTimeOffset? deadline = null)
    {
        // Compile once, with a match timeout, so a pathological pattern over large files can't
        // hang via catastrophic backtracking (ReDoS).
        var regex = new Regex(pattern, options, matchTimeout ?? TimeSpan.FromSeconds(2));
        var root = Path.GetFullPath(baseDirectory ?? Directory.GetCurrentDirectory());
        return BettererFileTest.Create(
            name,
            () => Scan(regex, root, includes),
            goal,
            deadline,
            fingerprint: () => BettererFileFingerprint.Compute(MatchedFiles(root, includes)));
    }

    private static IEnumerable<string> MatchedFiles(string root, IReadOnlyList<string> includes)
    {
        var matcher = new Matcher();
        foreach (var include in includes)
        {
            matcher.AddInclude(include);
        }

        return matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(root))).Files
            .Select(file => Path.Combine(root, file.Path));
    }

    private static BettererFileIssues Scan(Regex regex, string root, IReadOnlyList<string> includes)
    {
        var issues = new BettererFileIssues();

        var matcher = new Matcher();
        foreach (var include in includes)
        {
            matcher.AddInclude(include);
        }

        var matches = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(root)));
        foreach (var file in matches.Files)
        {
            // The matcher yields forward-slash relative paths; keep them for portable results.
            var path = file.Path.Replace('\\', '/');
            var text = File.ReadAllText(Path.Combine(root, file.Path));
            var lineStarts = ComputeLineStarts(text);

            foreach (Match match in regex.Matches(text))
            {
                var (line, column) = LineColumn(lineStarts, match.Index);
                issues.Add(path, line, column, match.Length, Summarize(match.Value));
            }
        }

        return issues;
    }

    private static int[] ComputeLineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                starts.Add(i + 1);
            }
        }

        return starts.ToArray();
    }

    private static (int Line, int Column) LineColumn(int[] lineStarts, int index)
    {
        // Largest line start <= index, via binary search. Line and column are 1-based.
        int low = 0, high = lineStarts.Length - 1, line = 0;
        while (low <= high)
        {
            var mid = (low + high) / 2;
            if (lineStarts[mid] <= index)
            {
                line = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return (line + 1, index - lineStarts[line] + 1);
    }

    private static string Summarize(string value)
    {
        var collapsed = Whitespace.Replace(value, " ").Trim();
        return collapsed.Length <= 80 ? collapsed : collapsed[..77] + "...";
    }
}
