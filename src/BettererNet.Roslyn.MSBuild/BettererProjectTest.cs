using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace BettererNet;

/// <summary>
/// File tests that analyse a real project or solution via <c>MSBuildWorkspace</c>, so diagnostics use
/// the project's actual references, build options, and full source set (unlike the source-path-based
/// <see cref="BettererRoslynTest"/>). Requires the .NET SDK at runtime (located by Microsoft.Build.Locator).
/// </summary>
public static class BettererProjectTest
{
    private static readonly object RegistrationGate = new();

    /// <summary>Track compiler diagnostics for a single <c>.csproj</c>. Defaults to warnings and errors.</summary>
    public static BettererTest<BettererFileIssues> FromProject(
        string name,
        string projectPath,
        Func<Diagnostic, bool>? filter = null,
        string? baseDirectory = null,
        Func<BettererFileIssues, bool>? goal = null,
        DateTimeOffset? deadline = null)
    {
        var matches = filter ?? (diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning);
        var baseDir = baseDirectory ?? Path.GetDirectoryName(Path.GetFullPath(projectPath))!;
        return BettererFileTest.Create(
            name,
            ct => AnalyzeProjectAsync(projectPath, matches, baseDir, ct),
            goal,
            deadline,
            fingerprint: () => BettererFileFingerprint.Compute(EnumerateInputs(projectPath)));
    }

    /// <summary>Track compiler diagnostics across every project in a <c>.sln</c>.</summary>
    public static BettererTest<BettererFileIssues> FromSolution(
        string name,
        string solutionPath,
        Func<Diagnostic, bool>? filter = null,
        string? baseDirectory = null,
        Func<BettererFileIssues, bool>? goal = null,
        DateTimeOffset? deadline = null)
    {
        var matches = filter ?? (diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning);
        var baseDir = baseDirectory ?? Path.GetDirectoryName(Path.GetFullPath(solutionPath))!;
        return BettererFileTest.Create(
            name,
            ct => AnalyzeSolutionAsync(solutionPath, matches, baseDir, ct),
            goal,
            deadline,
            fingerprint: () => BettererFileFingerprint.Compute(EnumerateInputs(solutionPath)));
    }

    private static async Task<BettererFileIssues> AnalyzeProjectAsync(string projectPath, Func<Diagnostic, bool> matches, string baseDir, CancellationToken ct)
    {
        EnsureLocatorRegistered();
        return await RunProjectAsync(projectPath, matches, baseDir, ct).ConfigureAwait(false);
    }

    private static async Task<BettererFileIssues> AnalyzeSolutionAsync(string solutionPath, Func<Diagnostic, bool> matches, string baseDir, CancellationToken ct)
    {
        EnsureLocatorRegistered();
        return await RunSolutionAsync(solutionPath, matches, baseDir, ct).ConfigureAwait(false);
    }

    private static void EnsureLocatorRegistered()
    {
        lock (RegistrationGate)
        {
            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
            }
        }
    }

    // NoInlining: keep MSBuild types out of the caller so the locator registers before they JIT-load.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<BettererFileIssues> RunProjectAsync(string projectPath, Func<Diagnostic, bool> matches, string baseDir, CancellationToken ct)
    {
        using var workspace = MSBuildWorkspace.Create();
        var project = await workspace.OpenProjectAsync(projectPath, cancellationToken: ct).ConfigureAwait(false);
        var issues = new BettererFileIssues();
        await AddDiagnosticsAsync(project, matches, baseDir, issues, ct).ConfigureAwait(false);
        return issues;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<BettererFileIssues> RunSolutionAsync(string solutionPath, Func<Diagnostic, bool> matches, string baseDir, CancellationToken ct)
    {
        using var workspace = MSBuildWorkspace.Create();
        var solution = await workspace.OpenSolutionAsync(solutionPath, cancellationToken: ct).ConfigureAwait(false);
        var issues = new BettererFileIssues();
        foreach (var project in solution.Projects)
        {
            await AddDiagnosticsAsync(project, matches, baseDir, issues, ct).ConfigureAwait(false);
        }

        return issues;
    }

    private static async Task AddDiagnosticsAsync(Project project, Func<Diagnostic, bool> matches, string baseDir, BettererFileIssues issues, CancellationToken ct)
    {
        var compilation = await project.GetCompilationAsync(ct).ConfigureAwait(false);
        if (compilation is null)
        {
            return;
        }

        foreach (var diagnostic in compilation.GetDiagnostics(ct).Where(matches))
        {
            var span = diagnostic.Location.GetLineSpan();
            if (!span.IsValid || string.IsNullOrEmpty(span.Path))
            {
                continue;
            }

            issues.Add(
                Relativize(span.Path, baseDir),
                span.StartLinePosition.Line + 1,
                span.StartLinePosition.Character + 1,
                diagnostic.Location.SourceSpan.Length,
                $"{diagnostic.Id}: {diagnostic.GetMessage(CultureInfo.InvariantCulture)}");
        }
    }

    private static string Relativize(string path, string baseDir) =>
        Path.GetRelativePath(baseDir, Path.GetFullPath(path)).Replace('\\', '/');

    // The cache fingerprint: the project/solution file plus its source (excluding build output).
    private static IEnumerable<string> EnumerateInputs(string projectOrSolutionPath)
    {
        var fullPath = Path.GetFullPath(projectOrSolutionPath);
        yield return fullPath;

        var directory = Path.GetDirectoryName(fullPath);
        if (directory is null)
        {
            yield break;
        }

        foreach (var source in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            if (!IsBuildOutput(source))
            {
                yield return source;
            }
        }
    }

    private static bool IsBuildOutput(string path)
    {
        var separator = Path.DirectorySeparatorChar;
        return path.Contains($"{separator}bin{separator}", StringComparison.Ordinal)
            || path.Contains($"{separator}obj{separator}", StringComparison.Ordinal);
    }
}
