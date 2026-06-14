using System.Text;

namespace BettererNet.Cli;

/// <summary>
/// Writes a markdown summary of the run (verdict, per-test table, and the new issues on a
/// regression) to a file. Intended to be posted as a PR comment by CI, e.g.
/// <c>gh pr comment --body-file betterer.md</c> or a sticky-comment action.
/// </summary>
public sealed class BettererMarkdownReporter : IBettererReporter
{
    private const int MaxIssuesShown = 50;

    private readonly string _path;
    private readonly List<BettererRunSummary> _runs = new();

    public BettererMarkdownReporter(string path) => _path = path;

    public void ReportRun(BettererRunSummary run) => _runs.Add(run);

    public void ReportSuite(BettererSuiteSummary suite)
    {
        var markdown = new StringBuilder();
        markdown.AppendLine("## Betterer");
        markdown.AppendLine();
        markdown.AppendLine(suite.IsFailure ? "**Result: FAILED** — something got worse." : "**Result: OK** — no regressions.");
        markdown.AppendLine();
        markdown.AppendLine("| Test | Status |");
        markdown.AppendLine("| --- | --- |");
        foreach (var run in suite.Runs)
        {
            markdown.AppendLine($"| {run.Name} | {run.Status.ToString().ToLowerInvariant()} |");
        }

        var newIssues = suite.Runs
            .SelectMany(run => BettererRunDiff.NewIssues(run).Select(item => (Test: run.Name, item.File, item.Issue)))
            .ToList();

        if (newIssues.Count > 0)
        {
            markdown.AppendLine();
            markdown.AppendLine("### New issues");
            foreach (var (test, file, issue) in newIssues.Take(MaxIssuesShown))
            {
                markdown.AppendLine($"- `{file}:{issue.Line}` ({test}) — {issue.Message}");
            }

            if (newIssues.Count > MaxIssuesShown)
            {
                markdown.AppendLine($"- ... and {newIssues.Count - MaxIssuesShown} more");
            }
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(_path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_path, markdown.ToString());
    }
}
