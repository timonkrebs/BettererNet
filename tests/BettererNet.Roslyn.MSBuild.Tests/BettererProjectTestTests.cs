using BettererNet;
using Microsoft.CodeAnalysis;
using Xunit;

namespace BettererNet.MSBuildTests;

public class BettererProjectTestTests
{
    // The sample project (restored as part of the solution build) is analysed via MSBuildWorkspace.
    private static string SampleProject =>
        Path.Combine(RepoRoot(), "samples", "SampleProject", "SampleProject.csproj");

    [Fact]
    public async Task FromProject_FindsNullableDiagnosticInRealProject()
    {
        // BadStuff.cs returns a possibly-null value from a non-nullable method (CS8603); finding it
        // requires the project's real nullable setting and references — i.e. the MSBuild workspace.
        var test = BettererProjectTest.FromProject("project", SampleProject, filter: d => d.Id == "CS8603");

        var summary = await test.RunAsync(null, new BettererRunContext());
        var issues = BettererFileIssuesSerializer.Instance.Deserialize(summary.Result);

        Assert.True(issues.TotalCount >= 1);
        Assert.Contains(issues.Files.Keys, file => file.EndsWith("BadStuff.cs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NullablePreset_FindsNullableWarnings()
    {
        var test = BettererNullableTest.Create("nullable", SampleProject);

        var summary = await test.RunAsync(null, new BettererRunContext());
        var issues = BettererFileIssuesSerializer.Instance.Deserialize(summary.Result);

        Assert.True(issues.TotalCount >= 1);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BettererNet.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
    }
}
