using Xunit;

namespace BettererNet.Tests;

public sealed class BettererSarifTestTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("betterernet").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string WriteSarif(string json)
    {
        var path = Path.Combine(_dir, "report.sarif");
        File.WriteAllText(path, json);
        return path;
    }

    private static async Task<BettererFileIssues> Run(BettererTest<BettererFileIssues> test) =>
        BettererFileIssuesSerializer.Instance.Deserialize((await test.RunAsync(null, new BettererRunContext())).Result);

    private const string Sample = """
        {
          "version": "2.1.0",
          "runs": [
            {
              "tool": { "driver": { "name": "TestAnalyzer" } },
              "results": [
                { "ruleId": "CA1822", "level": "warning", "message": { "text": "Mark static" },
                  "locations": [ { "physicalLocation": { "artifactLocation": { "uri": "src/Foo.cs" }, "region": { "startLine": 10, "startColumn": 5, "endColumn": 9 } } } ] },
                { "ruleId": "CS0168", "level": "error", "message": { "text": "Unused variable" },
                  "locations": [ { "physicalLocation": { "artifactLocation": { "uri": "src/Bar.cs" }, "region": { "startLine": 3 } } } ] },
                { "ruleId": "IDE0001", "level": "note", "message": { "text": "Simplify name" },
                  "locations": [ { "physicalLocation": { "artifactLocation": { "uri": "src/Foo.cs" }, "region": { "startLine": 1 } } } ] }
              ]
            }
          ]
        }
        """;

    [Fact]
    public async Task ImportsWarningsAndErrors_ExcludesNotes()
    {
        var issues = await Run(BettererSarifTest.Create("sarif", WriteSarif(Sample)));

        Assert.Equal(2, issues.TotalCount);
        Assert.True(issues.Files.ContainsKey("src/Foo.cs"));
        Assert.True(issues.Files.ContainsKey("src/Bar.cs"));
    }

    [Fact]
    public async Task ParsesLineColumnAndMessage()
    {
        var issues = await Run(BettererSarifTest.Create("sarif", WriteSarif(Sample)));

        var issue = Assert.Single(issues.Files["src/Foo.cs"]);
        Assert.Equal(10, issue.Line);
        Assert.Equal(5, issue.Column);
        Assert.Equal(4, issue.Length);
        Assert.Contains("CA1822", issue.Message);
    }

    [Fact]
    public async Task RespectsLevelFilter()
    {
        var issues = await Run(BettererSarifTest.Create("sarif", WriteSarif(Sample), levels: new HashSet<string> { "error" }));

        Assert.Equal(1, issues.TotalCount);
        Assert.True(issues.Files.ContainsKey("src/Bar.cs"));
    }

    [Fact]
    public async Task SkipsResultsWithoutLocation()
    {
        var json = """
            { "version": "2.1.0", "runs": [ { "results": [
              { "ruleId": "X", "level": "warning", "message": { "text": "no location" } }
            ] } ] }
            """;

        var issues = await Run(BettererSarifTest.Create("sarif", WriteSarif(json)));

        Assert.Equal(0, issues.TotalCount);
    }

    [Fact]
    public async Task NormalizesFileUriToLocalPath()
    {
        var json = """
            { "version": "2.1.0", "runs": [ { "results": [
              { "ruleId": "X", "level": "warning", "message": { "text": "m" },
                "locations": [ { "physicalLocation": { "artifactLocation": { "uri": "file:///work/src/App.cs" }, "region": { "startLine": 4 } } } ] }
            ] } ] }
            """;

        var issues = await Run(BettererSarifTest.Create("sarif", WriteSarif(json)));

        Assert.True(issues.Files.ContainsKey("/work/src/App.cs"));
    }

    [Fact]
    public async Task ClampsMissingRegionToLineOne()
    {
        var json = """
            { "version": "2.1.0", "runs": [ { "results": [
              { "ruleId": "X", "level": "warning", "message": { "text": "m" },
                "locations": [ { "physicalLocation": { "artifactLocation": { "uri": "A.cs" } } } ] }
            ] } ] }
            """;

        var issues = await Run(BettererSarifTest.Create("sarif", WriteSarif(json)));

        var issue = Assert.Single(issues.Files["A.cs"]);
        Assert.Equal(1, issue.Line);
        Assert.Equal(1, issue.Column);
    }
}
