using Xunit;

namespace BettererNet.Tests;

public sealed class BettererFormatTestTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("betterernet").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string WriteReport(string json)
    {
        var path = Path.Combine(_dir, "format-report.json");
        File.WriteAllText(path, json);
        return path;
    }

    private static async Task<BettererFileIssues> Run(BettererTest<BettererFileIssues> test) =>
        BettererFileIssuesSerializer.Instance.Deserialize((await test.RunAsync(null, new BettererRunContext())).Result);

    private const string Sample = """
        [
          {
            "DocumentId": { "ProjectId": { "Id": "p" }, "Id": "d" },
            "FileName": "Program.cs",
            "FilePath": "/work/src/Program.cs",
            "FileChanges": [
              { "LineNumber": 12, "CharNumber": 1, "DiagnosticId": "WHITESPACE", "FormatDescription": "Fix whitespace formatting." },
              { "LineNumber": 20, "CharNumber": 5, "DiagnosticId": "IDE0040", "FormatDescription": "Add accessibility modifiers." }
            ]
          },
          {
            "FileName": "Other.cs",
            "FilePath": "/work/src/Other.cs",
            "FileChanges": [
              { "LineNumber": 3, "CharNumber": 2, "DiagnosticId": "IMPORTS", "FormatDescription": "Sort imports." }
            ]
          }
        ]
        """;

    [Fact]
    public async Task ImportsEveryChangeAcrossFiles()
    {
        var issues = await Run(BettererFormatTest.Create("format", WriteReport(Sample)));

        Assert.Equal(3, issues.TotalCount);
        Assert.True(issues.Files.ContainsKey("/work/src/Program.cs"));
        Assert.True(issues.Files.ContainsKey("/work/src/Other.cs"));
    }

    [Fact]
    public async Task ParsesLineColumnAndMessage()
    {
        var issues = await Run(BettererFormatTest.Create("format", WriteReport(Sample)));

        var issue = Assert.Single(issues.Files["/work/src/Other.cs"]);
        Assert.Equal(3, issue.Line);
        Assert.Equal(2, issue.Column);
        Assert.Contains("IMPORTS", issue.Message);
        Assert.Contains("Sort imports", issue.Message);
    }

    [Fact]
    public async Task RespectsDiagnosticFilter()
    {
        var issues = await Run(BettererFormatTest.Create("format", WriteReport(Sample),
            diagnostics: new HashSet<string> { "WHITESPACE" }));

        var issue = Assert.Single(issues.Files["/work/src/Program.cs"]);
        Assert.Equal(12, issue.Line);
        Assert.Equal(1, issues.TotalCount);
    }

    [Fact]
    public async Task FallsBackToFileNameWhenNoFilePath()
    {
        var json = """
            [ { "FileName": "A.cs", "FileChanges": [
              { "LineNumber": 1, "CharNumber": 1, "DiagnosticId": "WHITESPACE", "FormatDescription": "x" } ] } ]
            """;

        var issues = await Run(BettererFormatTest.Create("format", WriteReport(json)));

        Assert.True(issues.Files.ContainsKey("A.cs"));
    }

    [Fact]
    public async Task ClampsMissingPositionToLineOne()
    {
        var json = """
            [ { "FilePath": "A.cs", "FileChanges": [
              { "DiagnosticId": "WHITESPACE", "FormatDescription": "x" } ] } ]
            """;

        var issues = await Run(BettererFormatTest.Create("format", WriteReport(json)));

        var issue = Assert.Single(issues.Files["A.cs"]);
        Assert.Equal(1, issue.Line);
        Assert.Equal(1, issue.Column);
    }

    [Fact]
    public async Task NonArrayReport_ReturnsNoIssues()
    {
        var issues = await Run(BettererFormatTest.Create("format", WriteReport("{}")));

        Assert.Equal(0, issues.TotalCount);
    }
}
