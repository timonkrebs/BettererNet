using Xunit;

namespace BettererNet.Tests;

public sealed class BettererRegexTestTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("betterernet").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void WriteFile(string name, string content) => File.WriteAllText(Path.Combine(_dir, name), content);

    private static async Task<BettererFileIssues> Run(BettererTest<BettererFileIssues> test) =>
        BettererFileIssuesSerializer.Instance.Deserialize((await test.RunAsync(null, new BettererRunContext())).Result);

    [Fact]
    public async Task CountsMatchesPerFile()
    {
        WriteFile("a.cs", "// TODO one\nvar x = 1; // TODO two\n");
        WriteFile("b.cs", "clean\n");

        var issues = await Run(BettererRegexTest.Create("todos", "TODO", new[] { "**/*.cs" }, _dir));

        Assert.Equal(2, issues.TotalCount);
        Assert.Equal(2, issues.Files["a.cs"].Count);
        Assert.False(issues.Files.ContainsKey("b.cs"));
    }

    [Fact]
    public async Task ReportsLineAndColumn()
    {
        WriteFile("a.cs", "line1\n  TODO here\n");

        var issues = await Run(BettererRegexTest.Create("todos", "TODO", new[] { "**/*.cs" }, _dir));

        var issue = Assert.Single(issues.Files["a.cs"]);
        Assert.Equal(2, issue.Line);
        Assert.Equal(3, issue.Column);
    }

    [Fact]
    public async Task RespectsGlobIncludes()
    {
        WriteFile("a.cs", "TODO\n");
        WriteFile("a.txt", "TODO\n");

        var issues = await Run(BettererRegexTest.Create("todos", "TODO", new[] { "**/*.cs" }, _dir));

        Assert.Equal(1, issues.TotalCount);
    }

    [Fact]
    public async Task NoMatches_IsClean()
    {
        WriteFile("a.cs", "nothing to see here\n");

        var issues = await Run(BettererRegexTest.Create("todos", "TODO", new[] { "**/*.cs" }, _dir));

        Assert.Equal(0, issues.TotalCount);
    }
}
