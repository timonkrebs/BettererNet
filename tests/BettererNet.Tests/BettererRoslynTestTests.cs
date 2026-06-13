using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace BettererNet.Tests;

public sealed class BettererRoslynTestTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("betterernet").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string WriteSource(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static async Task<BettererFileIssues> Run(BettererTest<BettererFileIssues> test) =>
        BettererFileIssuesSerializer.Instance.Deserialize((await test.RunAsync(null, new BettererRunContext())).Result);

    [Fact]
    public async Task Diagnostics_FindsNullableWarnings()
    {
        var path = WriteSource("N.cs", "class C { string F() { string x = null; return x; } }");

        var test = BettererRoslynTest.Diagnostics(
            "nullable",
            new[] { path },
            filter: diagnostic => diagnostic.Id.StartsWith("CS8", StringComparison.Ordinal),
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var issues = await Run(test);

        Assert.True(issues.TotalCount >= 1);
    }

    [Fact]
    public async Task Diagnostics_CleanCode_IsClean()
    {
        var path = WriteSource("Clean.cs", "class C { int F() { return 1; } }");

        var test = BettererRoslynTest.Diagnostics(
            "clean",
            new[] { path },
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var issues = await Run(test);

        Assert.Equal(0, issues.TotalCount);
    }

    [Fact]
    public async Task SyntaxQuery_CountsMatchingNodes()
    {
        var path = WriteSource("Three.cs", "class A {} class B {} class C {}");

        var test = BettererRoslynTest.SyntaxQuery(
            "classes",
            new[] { path },
            node => node is ClassDeclarationSyntax,
            describe: node => ((ClassDeclarationSyntax)node).Identifier.Text);

        var issues = await Run(test);

        Assert.Equal(3, issues.TotalCount);
    }

    [Fact]
    public async Task Analyzers_ReportDiagnostics()
    {
        var path = WriteSource("Two.cs", "class A {} class B {}");
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new ClassDeclarationAnalyzer());

        var issues = await Run(BettererRoslynTest.Analyzers("classes", new[] { path }, analyzers));

        Assert.Equal(2, issues.TotalCount);
    }
}

/// <summary>A trivial analyzer that flags every class declaration, used to exercise the analyzer path.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ClassDeclarationAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        "TEST001",
        "Class declaration",
        "Class '{0}'",
        "Test",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            analysis =>
            {
                var declaration = (ClassDeclarationSyntax)analysis.Node;
                analysis.ReportDiagnostic(Diagnostic.Create(Rule, declaration.Identifier.GetLocation(), declaration.Identifier.Text));
            },
            SyntaxKind.ClassDeclaration);
    }
}
