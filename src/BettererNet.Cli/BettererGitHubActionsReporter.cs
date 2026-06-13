using System.Text;

namespace BettererNet.Cli;

/// <summary>
/// Reporter for GitHub Actions: emits <c>::error::</c> workflow annotations for failing tests and
/// appends a markdown summary table to the job's step summary (<c>$GITHUB_STEP_SUMMARY</c>).
/// </summary>
public sealed class BettererGitHubActionsReporter : IBettererReporter
{
    private readonly TextWriter _output;
    private readonly string? _stepSummaryPath;

    public BettererGitHubActionsReporter(TextWriter? output = null, string? stepSummaryPath = null)
    {
        _output = output ?? Console.Out;
        _stepSummaryPath = stepSummaryPath ?? Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
    }

    public void ReportRun(BettererRunSummary run)
    {
        if (run.Status is BettererRunStatus.Worse or BettererRunStatus.Expired or BettererRunStatus.Failed)
        {
            _output.WriteLine($"::error title=Betterer::{Escape($"{run.Name} got {Describe(run.Status)}")}");
        }
    }

    public void ReportSuite(BettererSuiteSummary suite)
    {
        if (string.IsNullOrEmpty(_stepSummaryPath))
        {
            return;
        }

        var summary = new StringBuilder();
        summary.AppendLine("## Betterer");
        summary.AppendLine();
        summary.AppendLine(suite.IsFailure ? "**Result: FAILED**" : "**Result: OK**");
        summary.AppendLine();
        summary.AppendLine("| Test | Status |");
        summary.AppendLine("| --- | --- |");
        foreach (var run in suite.Runs)
        {
            summary.AppendLine($"| {run.Name} | {Describe(run.Status)} |");
        }

        summary.AppendLine();

        try
        {
            File.AppendAllText(_stepSummaryPath, summary.ToString());
        }
        catch
        {
            // Best effort: a missing/unwritable step-summary file shouldn't fail the run.
        }
    }

    private static string Describe(BettererRunStatus status) => status.ToString().ToLowerInvariant();

    // Escape per the GitHub Actions workflow-command spec.
    private static string Escape(string value) =>
        value.Replace("%", "%25").Replace("\r", "%0D").Replace("\n", "%0A");
}
