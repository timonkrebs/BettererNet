using BettererNet.Cli;
using Xunit;

namespace BettererNet.Tests;

public sealed class BettererConfigFileTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("betterernet").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static async Task<BettererFileIssues> Run(IBettererTest test) =>
        BettererFileIssuesSerializer.Instance.Deserialize((await test.RunAsync(null, new BettererRunContext())).Result);

    [Fact]
    public void Load_ParsesAllDeclarativeTypes()
    {
        var json = Write("betterer.json", """
            {
              "results": ".betterer.results",
              "tests": {
                "Todos": { "type": "regex", "pattern": "TODO", "includes": ["**/*.cs"] },
                "Cov": { "type": "coverage", "report": "coverage.cobertura.xml" },
                "Sarif": { "type": "sarif", "report": "a.sarif", "levels": ["error"] }
              }
            }
            """);

        var (tests, results) = BettererConfigFile.Load(json);

        Assert.Equal(3, tests.Count);
        Assert.Equal(new[] { "Cov", "Sarif", "Todos" }, tests.Select(test => test.Name).OrderBy(name => name));
        Assert.EndsWith(".betterer.results", results);
    }

    [Fact]
    public async Task RegexTest_FromConfig_FindsMatches()
    {
        Write("src/a.cs", "// TODO one\n// TODO two\n");
        var json = Write("betterer.json", """
            { "tests": { "Todos": { "type": "regex", "pattern": "TODO", "includes": ["**/*.cs"] } } }
            """);

        var (tests, _) = BettererConfigFile.Load(json);
        var issues = await Run(Assert.Single(tests));

        Assert.Equal(2, issues.TotalCount);
    }

    [Fact]
    public void UnknownType_Throws()
    {
        var json = Write("betterer.json", """{ "tests": { "X": { "type": "teamcity" } } }""");

        Assert.Throws<InvalidOperationException>(() => BettererConfigFile.Load(json));
    }

    [Fact]
    public void MissingRequiredField_Throws()
    {
        var json = Write("betterer.json", """{ "tests": { "X": { "type": "regex" } } }""");

        Assert.Throws<InvalidOperationException>(() => BettererConfigFile.Load(json));
    }

    [Fact]
    public void ResultsPath_ResolvedRelativeToConfig()
    {
        var json = Write("betterer.json", """{ "results": ".betterer.results", "tests": {} }""");

        var (_, results) = BettererConfigFile.Load(json);

        Assert.Equal(Path.Combine(_dir, ".betterer.results"), results);
    }

    [Fact]
    public void LoadsJsoncWithCommentsAndTrailingCommas()
    {
        var json = Write("betterer.jsonc", """
            {
              // a comment
              "tests": {
                "Todos": { "type": "regex", "pattern": "TODO", "includes": ["**/*.cs"] },
              },
            }
            """);

        var (tests, _) = BettererConfigFile.Load(json);

        Assert.Single(tests);
    }

    [Fact]
    public void TestsNotObject_Throws()
    {
        var json = Write("betterer.json", """{ "tests": [] }""");

        Assert.Throws<InvalidOperationException>(() => BettererConfigFile.Load(json));
    }

    [Fact]
    public void TestEntryNotObject_Throws()
    {
        var json = Write("betterer.json", """{ "tests": { "X": "oops" } }""");

        Assert.Throws<InvalidOperationException>(() => BettererConfigFile.Load(json));
    }
}
