using BettererNet.Cli;
using Xunit;

namespace BettererNet.Tests;

public sealed class BettererSarifReporterTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("betterernet").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task WritesSarifReport_ThatRoundTripsThroughImport()
    {
        var path = Path.Combine(_dir, "out.sarif");
        var reporter = new BettererSarifReporter(path);
        var result = BettererFileIssuesSerializer.Instance.Serialize(
            new BettererFileIssues().Add("Foo.cs", 10, 5, 3, "CA1822: mark static").Add("Bar.cs", 1, 1, 0, "issue"));
        var run = new BettererRunSummary { Name = "Analyzers", Status = BettererRunStatus.New, Result = result };

        reporter.ReportRun(run);
        reporter.ReportSuite(new BettererSuiteSummary { Runs = new[] { run } });

        Assert.True(File.Exists(path));
        var text = await File.ReadAllTextAsync(path);
        Assert.Contains("\"version\": \"2.1.0\"", text);
        Assert.Contains("CA1822", text);

        // Re-importing the exported SARIF yields the same two issues.
        var reimported = BettererFileIssuesSerializer.Instance.Deserialize(
            (await BettererSarifTest.Create("x", path).RunAsync(null, new BettererRunContext())).Result);
        Assert.Equal(2, reimported.TotalCount);
    }
}
