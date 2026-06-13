using BettererNet.Cli;
using Xunit;

namespace BettererNet.Tests;

public sealed class BettererGitHubActionsReporterTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("betterernet").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void EmitsErrorAnnotationsAndStepSummary()
    {
        var output = new StringWriter();
        var summaryPath = Path.Combine(_dir, "summary.md");
        var reporter = new BettererGitHubActionsReporter(output, summaryPath);

        var worse = new BettererRunSummary { Name = "BadTest", Status = BettererRunStatus.Worse };
        var same = new BettererRunSummary { Name = "GoodTest", Status = BettererRunStatus.Same };
        reporter.ReportRun(worse);
        reporter.ReportRun(same);
        reporter.ReportSuite(new BettererSuiteSummary { Runs = new[] { worse, same } });

        var annotations = output.ToString();
        Assert.Contains("::error", annotations);
        Assert.Contains("BadTest got worse", annotations);
        Assert.DoesNotContain("GoodTest", annotations); // passing tests get no error annotation

        var summary = File.ReadAllText(summaryPath);
        Assert.Contains("FAILED", summary);
        Assert.Contains("| BadTest | worse |", summary);
        Assert.Contains("| GoodTest | same |", summary);
    }
}
