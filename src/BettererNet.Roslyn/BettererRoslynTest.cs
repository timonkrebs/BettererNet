using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace BettererNet;

/// <summary>
/// Factories for Roslyn-based file tests over C# source: compiler diagnostics (the analog of
/// betterer's <c>typescript</c> test, e.g. adopting nullable reference types incrementally),
/// analyzer diagnostics (the <c>eslint</c> analog), and syntax queries (the <c>tsquery</c> analog).
/// </summary>
public static class BettererRoslynTest
{
    private static readonly Lazy<IReadOnlyList<MetadataReference>> DefaultReferences = new(() =>
        ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray());

    /// <summary>Track C# compiler diagnostics. Defaults to warnings and errors.</summary>
    public static BettererTest<BettererFileIssues> Diagnostics(
        string name,
        IReadOnlyList<string> sourcePaths,
        Func<Diagnostic, bool>? filter = null,
        CSharpCompilationOptions? compilationOptions = null,
        CSharpParseOptions? parseOptions = null,
        IEnumerable<MetadataReference>? references = null,
        Func<BettererFileIssues, bool>? goal = null,
        DateTimeOffset? deadline = null)
    {
        var matches = filter ?? (diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning);
        return BettererFileTest.Create(name, async cancellationToken =>
        {
            var compilation = await CompileAsync(name, sourcePaths, compilationOptions, parseOptions, references, cancellationToken)
                .ConfigureAwait(false);
            return ToIssues(compilation.GetDiagnostics(cancellationToken).Where(matches));
        }, goal, deadline);
    }

    /// <summary>Track diagnostics reported by the given Roslyn analyzers.</summary>
    public static BettererTest<BettererFileIssues> Analyzers(
        string name,
        IReadOnlyList<string> sourcePaths,
        ImmutableArray<DiagnosticAnalyzer> analyzers,
        Func<Diagnostic, bool>? filter = null,
        CSharpCompilationOptions? compilationOptions = null,
        CSharpParseOptions? parseOptions = null,
        IEnumerable<MetadataReference>? references = null,
        Func<BettererFileIssues, bool>? goal = null,
        DateTimeOffset? deadline = null)
    {
        var matches = filter ?? (_ => true);
        return BettererFileTest.Create(name, async cancellationToken =>
        {
            var compilation = await CompileAsync(name, sourcePaths, compilationOptions, parseOptions, references, cancellationToken)
                .ConfigureAwait(false);
            var withAnalyzers = compilation.WithAnalyzers(analyzers, options: null);
            var diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
            return ToIssues(diagnostics.Where(matches));
        }, goal, deadline);
    }

    /// <summary>Count syntax nodes matching <paramref name="predicate"/> — the .NET answer to tsquery.</summary>
    public static BettererTest<BettererFileIssues> SyntaxQuery(
        string name,
        IReadOnlyList<string> sourcePaths,
        Func<SyntaxNode, bool> predicate,
        Func<SyntaxNode, string>? describe = null,
        CSharpParseOptions? parseOptions = null,
        Func<BettererFileIssues, bool>? goal = null,
        DateTimeOffset? deadline = null)
    {
        var description = describe ?? (node => node.Kind().ToString());
        return BettererFileTest.Create(name, async cancellationToken =>
        {
            var issues = new BettererFileIssues();
            foreach (var path in sourcePaths)
            {
                var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                var tree = CSharpSyntaxTree.ParseText(text, parseOptions, path: Normalize(path), cancellationToken: cancellationToken);
                var root = await tree.GetRootAsync(cancellationToken).ConfigureAwait(false);

                foreach (var node in root.DescendantNodes().Where(predicate))
                {
                    var span = node.GetLocation().GetLineSpan();
                    issues.Add(
                        Normalize(path),
                        span.StartLinePosition.Line + 1,
                        span.StartLinePosition.Character + 1,
                        node.Span.Length,
                        description(node));
                }
            }

            return issues;
        }, goal, deadline);
    }

    private static async Task<CSharpCompilation> CompileAsync(
        string assemblyName,
        IReadOnlyList<string> sourcePaths,
        CSharpCompilationOptions? compilationOptions,
        CSharpParseOptions? parseOptions,
        IEnumerable<MetadataReference>? references,
        CancellationToken cancellationToken)
    {
        var trees = new List<SyntaxTree>(sourcePaths.Count);
        foreach (var path in sourcePaths)
        {
            var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            trees.Add(CSharpSyntaxTree.ParseText(text, parseOptions, path: Normalize(path), cancellationToken: cancellationToken));
        }

        return CSharpCompilation.Create(
            assemblyName,
            trees,
            references ?? DefaultReferences.Value,
            compilationOptions ?? new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static BettererFileIssues ToIssues(IEnumerable<Diagnostic> diagnostics)
    {
        var issues = new BettererFileIssues();
        foreach (var diagnostic in diagnostics)
        {
            var span = diagnostic.Location.GetLineSpan();
            if (!span.IsValid || string.IsNullOrEmpty(span.Path))
            {
                continue;
            }

            issues.Add(
                Normalize(span.Path),
                span.StartLinePosition.Line + 1,
                span.StartLinePosition.Character + 1,
                diagnostic.Location.SourceSpan.Length,
                $"{diagnostic.Id}: {diagnostic.GetMessage(CultureInfo.InvariantCulture)}");
        }

        return issues;
    }

    private static string Normalize(string path) => path.Replace('\\', '/');
}
