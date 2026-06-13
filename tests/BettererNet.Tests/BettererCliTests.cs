using System.Text.Json.Nodes;
using BettererNet.Cli;
using Xunit;

namespace BettererNet.Tests;

public sealed class BettererCliTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("betterernet").FullName;

    private string ResultsPath => Path.Combine(_dir, BettererResultsFile.DefaultFileName);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private BettererCliOptions Options(bool update = false, int workers = 1, params string[] filters) => new()
    {
        ResultsPath = ResultsPath,
        Update = update,
        Workers = workers,
        Filters = filters,
        Reporter = new FakeReporter(),
    };

    private async Task Seed(string name, long value)
    {
        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        file.Set(name, JsonValue.Create(value));
        await file.SaveAsync();
    }

    private static IBettererTest Count(string name, long value) => BettererCountTest.Create(name, () => value);

    [Fact]
    public void Parse_ReadsCommandAndOptions()
    {
        var (command, options, error) = BettererCli.Parse(
            ["ci", "--results", "x.results", "-f", "Foo", "--update", "--strict", "--silent"]);

        Assert.Null(error);
        Assert.Equal("ci", command);
        Assert.Equal("x.results", options.ResultsPath);
        Assert.Equal(new[] { "Foo" }, options.Filters);
        Assert.True(options.Update);
        Assert.True(options.Strict);
        Assert.True(options.Silent);
    }

    [Fact]
    public void Parse_DefaultsToStart()
    {
        var (command, _, error) = BettererCli.Parse([]);

        Assert.Null(error);
        Assert.Equal("start", command);
    }

    [Fact]
    public void Parse_UnknownOption_ReturnsError()
    {
        var (_, _, error) = BettererCli.Parse(["--bogus"]);

        Assert.NotNull(error);
    }

    [Fact]
    public async Task Start_RecordsNewTests_AndReturnsZero()
    {
        var code = await BettererCli.StartAsync([Count("a", 3)], Options());

        Assert.Equal(0, code);
        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        Assert.True(file.TryGet("a", out _));
    }

    [Fact]
    public async Task Start_FailsOnRegression_AndKeepsBaseline()
    {
        await Seed("a", 2);

        var code = await BettererCli.StartAsync([Count("a", 5)], Options());

        Assert.Equal(1, code);
        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        Assert.Equal(2, file.Results["a"].GetValue<long>());
    }

    [Fact]
    public async Task Start_Update_AcceptsRegression()
    {
        await Seed("a", 2);

        var code = await BettererCli.StartAsync([Count("a", 5)], Options(update: true));

        Assert.Equal(0, code);
        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        Assert.Equal(5, file.Results["a"].GetValue<long>());
    }

    [Fact]
    public async Task Ci_PassesWhenAllSame()
    {
        await Seed("a", 3);

        var code = await BettererCli.CiAsync([Count("a", 3)], Options());

        Assert.Equal(0, code);
    }

    [Fact]
    public async Task Ci_FailsOnNewResults_AndDoesNotWrite()
    {
        var code = await BettererCli.CiAsync([Count("a", 3)], Options());

        Assert.Equal(1, code);
        Assert.False(File.Exists(ResultsPath));
    }

    [Fact]
    public async Task Filter_RunsOnlyMatchingTests()
    {
        await BettererCli.StartAsync([Count("alpha", 1), Count("beta", 1)], Options(filters: "alpha"));

        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        Assert.True(file.TryGet("alpha", out _));
        Assert.False(file.TryGet("beta", out _));
    }

    [Fact]
    public async Task Filter_Negation_ExcludesTests()
    {
        await BettererCli.StartAsync([Count("alpha", 1), Count("beta", 1)], Options(filters: "!beta"));

        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        Assert.True(file.TryGet("alpha", out _));
        Assert.False(file.TryGet("beta", out _));
    }

    [Fact]
    public void Parse_ReadsWorkers()
    {
        var (_, options, error) = BettererCli.Parse(["start", "--workers", "4"]);

        Assert.Null(error);
        Assert.Equal(4, options.Workers);
    }

    [Fact]
    public void Parse_InvalidWorkers_ReturnsError()
    {
        var (_, _, error) = BettererCli.Parse(["--workers", "zero"]);

        Assert.NotNull(error);
    }

    [Fact]
    public async Task Workers_RunsAllTestsCorrectly()
    {
        var tests = Enumerable.Range(0, 10).Select(i => Count($"t{i:D2}", i)).ToArray();

        var code = await BettererCli.StartAsync(tests, Options(workers: 4));

        Assert.Equal(0, code);
        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        Assert.Equal(10, file.Results.Count);
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(i, file.Results[$"t{i:D2}"].GetValue<long>());
        }
    }

    [Fact]
    public async Task Merge_CombinesResultsFiles()
    {
        var oursPath = Path.Combine(_dir, "ours.results");
        var theirsPath = Path.Combine(_dir, "theirs.results");
        var ours = BettererResultsFile.Create(oursPath);
        ours.Set("a", JsonValue.Create(3L));
        await ours.SaveAsync();
        var theirs = BettererResultsFile.Create(theirsPath);
        theirs.Set("a", JsonValue.Create(5L));
        await theirs.SaveAsync();

        var code = await BettererCli.MergeAsync(["merge", oursPath, theirsPath]);

        Assert.Equal(0, code);
        var merged = await BettererResultsFile.LoadAsync(oursPath);
        Assert.Equal(3, merged.Results["a"].GetValue<long>());
    }

    [Fact]
    public void Init_Automerge_WritesGitAttributes()
    {
        var code = BettererCli.Init(_dir, automerge: true);

        Assert.Equal(0, code);
        Assert.True(File.Exists(Path.Combine(_dir, "BettererConfig.cs")));
        Assert.Contains("merge=betterer", File.ReadAllText(Path.Combine(_dir, ".gitattributes")));
    }

    private sealed class FakeReporter : IBettererReporter
    {
        public void ReportRun(BettererRunSummary run)
        {
        }

        public void ReportSuite(BettererSuiteSummary suite)
        {
        }
    }
}
