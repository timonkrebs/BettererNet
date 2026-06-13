using Xunit;

namespace BettererNet.Tests;

public sealed class BettererCoverageTestTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("betterernet").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string WriteReport(string xml)
    {
        var path = Path.Combine(_dir, "coverage.cobertura.xml");
        File.WriteAllText(path, xml);
        return path;
    }

    private static async Task<BettererFileIssues> Run(BettererTest<BettererFileIssues> test) =>
        BettererFileIssuesSerializer.Instance.Deserialize((await test.RunAsync(null, new BettererRunContext())).Result);

    [Fact]
    public async Task CountsUncoveredLinesPerFile()
    {
        var path = WriteReport("""
            <coverage>
              <packages><package name="P"><classes>
                <class name="A" filename="src/A.cs"><lines>
                  <line number="1" hits="1"/><line number="2" hits="0"/><line number="3" hits="0"/>
                </lines></class>
                <class name="B" filename="src/B.cs"><lines>
                  <line number="10" hits="5"/>
                </lines></class>
              </classes></package></packages>
            </coverage>
            """);

        var issues = await Run(BettererCoverageTest.Create("cov", path));

        Assert.Equal(2, issues.TotalCount);
        Assert.Equal(2, issues.Files["src/A.cs"].Count);
        Assert.False(issues.Files.ContainsKey("src/B.cs"));
    }

    [Fact]
    public async Task FullyCovered_IsClean()
    {
        var path = WriteReport("""
            <coverage><packages><package name="P"><classes>
              <class name="A" filename="A.cs"><lines><line number="1" hits="2"/></lines></class>
            </classes></package></packages></coverage>
            """);

        var issues = await Run(BettererCoverageTest.Create("cov", path));

        Assert.Equal(0, issues.TotalCount);
    }

    [Fact]
    public async Task DedupesLinesSharedAcrossClasses()
    {
        var path = WriteReport("""
            <coverage><packages><package name="P"><classes>
              <class name="A1" filename="A.cs"><lines><line number="5" hits="0"/></lines></class>
              <class name="A2" filename="A.cs"><lines><line number="5" hits="0"/></lines></class>
            </classes></package></packages></coverage>
            """);

        var issues = await Run(BettererCoverageTest.Create("cov", path));

        Assert.Equal(1, issues.TotalCount);
    }
}
