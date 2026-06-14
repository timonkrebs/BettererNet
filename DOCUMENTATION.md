# BettererNet — User Documentation

BettererNet tracks the *amount* of something undesirable in your codebase (architecture
violations, analyzer warnings, uncovered lines, banned patterns, …), records it as a **baseline**,
and then **fails when it gets worse** while **locking in every improvement**. It's the .NET
counterpart of [`betterer`](https://github.com/phenomnomnominal/betterer).

> Status: pre-release. Consume the projects via project/source reference for now; NuGet packages
> are planned.

## Table of contents

1. [Core idea](#core-idea)
2. [Prerequisites & projects](#prerequisites--projects)
3. [Quick start (xUnit)](#quick-start-xunit)
4. [Run states](#run-states)
5. [Built-in test types](#built-in-test-types)
6. [Writing a custom test](#writing-a-custom-test)
7. [The results file](#the-results-file)
8. [Seeding a baseline](#seeding-a-baseline)
9. [The CLI](#the-cli)
10. [Merge & automerge](#merge--automerge)
11. [Reporters & CI](#reporters--ci)
12. [FAQ](#faq)

---

## Core idea

A Betterer test produces a value (usually a set of issues, or a count). On the first accepted run,
that value becomes the **baseline**, stored in a `.betterer.results` file you commit. On later runs:

- **worse than baseline** → the test **fails** (the baseline is not changed);
- **better than baseline** → the test **passes** and the baseline **ratchets down** to the new value;
- **same** → passes, nothing is written.

This lets a large team agree "no new violations of X, and chip away at the existing ones" and have
it enforced automatically — without a long-lived branch.

## Prerequisites & projects

- **.NET SDK 10.0** or newer.

| Project | Purpose |
|---|---|
| `BettererNet.Core` | The engine: tests, constraints, goals, results file, runner. |
| `BettererNet.Xunit` | Assert against the baseline from xUnit tests (`dotnet test`). |
| `BettererNet.NUnit` | The same `new Betterer().AssertAsync(...)` API, for NUnit tests. |
| `BettererNet.MSTest` | The same, for MSTest tests. |
| `BettererNet.Cli` | The `betterernet` tool (`init`/`start`/`ci`/`watch`/`precommit`/`results`/`merge`). |
| `BettererNet.Regex` | `BettererRegexTest` — count regex matches across files. |
| `BettererNet.Roslyn` | `BettererRoslynTest` — compiler diagnostics, analyzers, syntax queries. |
| `BettererNet.Roslyn.MSBuild` | `BettererProjectTest` — analyse a real `.csproj`/`.sln` via MSBuildWorkspace; `BettererNullableTest` — nullable-adoption preset. |
| `BettererNet.Coverage` | `BettererCoverageTest` — uncovered lines from a Cobertura report. |
| `BettererNet.NetArchTest` | `BettererArchTest` — wrap a NetArchTest rule. |
| `BettererNet.Sarif` | `BettererSarifTest` — import any SARIF report. |
| `BettererNet.Format` | `BettererFormatTest` — track a `dotnet format` report (whitespace/imports/style). |

All test types live in the `BettererNet` namespace.

## Quick start (xUnit)

Reference `BettererNet.Xunit` (and the integration package you need) from your test project, then
assert against the baseline:

```csharp
using BettererNet;
using NetArchTest.Rules;
using Xunit;

public class ArchitectureTests
{
    private static readonly System.Reflection.Assembly Target = typeof(MyMarker).Assembly;

    [Fact]
    public async Task InterfacesStartWithI()
    {
        await new Betterer().AssertAsync(BettererArchTest.Create("InterfacesStartWithI", () =>
            Types.InAssembly(Target).That().AreInterfaces()
                .Should().HaveNameStartingWith("I").GetResult()));
    }
}
```

`new Betterer()` stores the result under the **calling method's name** in `.betterer.results` in the
test project directory. Override either with the constructor:

```csharp
new Betterer(testName: "CustomName", resultsPath: "/path/to/.betterer.results")
```

There are two `AssertAsync` overloads:

- `AssertAsync(BettererResult result, bool allowFirstFailure = false)` — `BettererResult` carries a
  `List<string> FailingTypeNames`; new names fail, removed names ratchet down.
- `AssertAsync(IBettererTest test, bool allowFirstFailure = false)` — run any engine test
  (counting, file, regex, Roslyn, coverage, SARIF, custom). The test's own name is the key.

`allowFirstFailure: true` records the reported issues as the baseline on the first run instead of
failing.

## Run states

Every run resolves to one of these (surfaced on `BettererRunSummary.Status`):

| State | Meaning | Baseline | xUnit result |
|---|---|---|---|
| `New` | first run, no baseline | recorded (unless it fails first) | fails unless `allowFirstFailure`/`BETTERER_UPDATE` |
| `Better` | improved | ratcheted down | passes |
| `Same` | unchanged | untouched | passes |
| `Worse` | regressed | **unchanged** | **fails** |
| `Complete` | met its goal | recorded | passes |
| `Updated` | worse, but update was requested | overwritten | passes |
| `Skipped` | not run | untouched | passes |
| `Failed` | the test function threw | untouched | fails (rethrows) |
| `Expired` | deadline passed without meeting the goal | progress kept | fails |

## Built-in test types

Each factory returns an `IBettererTest` you pass to `AssertAsync` (xUnit) or return from a CLI
config (see [The CLI](#the-cli)). `goal` and `deadline` are optional on all of them.

### Regex

Count matches of a pattern across globbed files — ban an API or burn down TODOs.

```csharp
BettererRegexTest.Create("NoConsoleWriteLine", @"Console\.WriteLine", new[] { "**/*.cs" });
// signature: Create(name, pattern, includes, baseDirectory?, options?, matchTimeout?, goal?, deadline?)
```

### Roslyn (compiler diagnostics, analyzers, syntax queries)

```csharp
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Compiler diagnostics — adopt nullable reference types incrementally.
BettererRoslynTest.Diagnostics("Nullable", sourceFiles,
    filter: d => d.Id.StartsWith("CS8"),
    compilationOptions: new CSharpCompilationOptions(
        OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

// Analyzers — track any DiagnosticAnalyzer's findings.
BettererRoslynTest.Analyzers("Style", sourceFiles, analyzers);

// Syntax query — count nodes you want to eliminate (the tsquery analog).
BettererRoslynTest.SyntaxQuery("NoGoto", sourceFiles, node => node is GotoStatementSyntax);
```

`sourceFiles` is a list of `.cs` paths — a fast approximation that uses the host's references, fine
for syntax queries. For **accurate** diagnostics (correct references, your project's nullable/build
settings, the full source set, source generators), analyse the real project with
`BettererNet.Roslyn.MSBuild` instead:

```csharp
// Whole-project / whole-solution analysis via MSBuildWorkspace (needs the .NET SDK at runtime).
await new Betterer().AssertAsync(BettererProjectTest.FromProject(
    "Nullable", "src/App/App.csproj", filter: d => d.Id.StartsWith("CS8")));

await new Betterer().AssertAsync(BettererProjectTest.FromSolution("Warnings", "App.sln"));
```

#### Nullable-adoption preset

Enabling nullable reference types on a mature codebase floods the build with `CS8600`–`CS86xx`
warnings. `BettererNullableTest` is a turnkey wrapper over `FromProject` that filters to exactly those
warnings with a goal of zero — set `<Nullable>enable</Nullable>` in the project, baseline the existing
warnings, then burn them down without ever letting new ones in:

```csharp
// Tracks every nullable warning in the project; goal defaults to zero (fully adopted).
await new Betterer().AssertAsync(BettererNullableTest.Create("Nullable", "src/App/App.csproj"));
```

Pass `goal:` / `deadline:` to set an intermediate target or a date by which adoption must complete.

### Coverage

Track uncovered lines from a Cobertura report (e.g. `dotnet test --collect:"XPlat Code Coverage"`):

```csharp
BettererCoverageTest.Create("Coverage", "coverage.cobertura.xml", goal: BettererFileTest.NoIssues);
```

### Architecture (NetArchTest)

```csharp
BettererArchTest.Create("Layering", () =>
    Types.InAssembly(assembly).That().ResideInNamespace("App.Domain")
        .ShouldNot().HaveDependencyOn("App.Infrastructure").GetResult());
```

### SARIF

Import any SARIF 2.1.0 report — the whole static-analysis ecosystem:

```csharp
BettererSarifTest.Create("Analyzers", "analysis.sarif");
// optional: levels (default {"warning","error"}), e.g. new HashSet<string> { "error" }
```

### dotnet format

Adopt strict formatting and style incrementally — the .NET answer to enabling a lint config one rule
at a time. Run `dotnet format` in *report-only* mode in CI so it lists, but doesn't apply, every
whitespace, import-ordering, and analyzer/style fix; then ingest the report:

```bash
dotnet format --verify-no-changes --report format-report.json
```

```csharp
BettererFormatTest.Create("Format", "format-report.json");
// optional: diagnostics — track only certain DiagnosticIds, e.g. new HashSet<string> { "WHITESPACE" }
```

Baseline the report, then tighten `.editorconfig` and burn the findings down without ever letting new
ones in.

### Counting

When you just have a number to drive down:

```csharp
BettererCountTest.Create("OpenTodos", CountTodos, goal: 0);
```

## Writing a custom test

The generic primitive is `BettererTest<T>`:

```csharp
new BettererTest<long>(
    name: "MyMetric",
    test: () => MeasureSomething(),        // Func<T> or Func<CancellationToken, Task<T>>
    constraint: BettererConstraints.Smaller, // compares current vs baseline
    goal: value => value == 0,             // optional
    deadline: new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero)); // optional
```

Built-in constraints: `BettererConstraints.Smaller` and `Bigger` (for `long`), and
`BettererConstraints.SetBased<T>()` (any new element is a regression). Provide your own
`BettererConstraint<T>` delegate `(current, baseline) => BettererConstraintResult` for custom logic.
Values are serialized to the results file via `IBettererSerializer<T>` (JSON by default).

For file-oriented tests, produce a `BettererFileIssues`:

```csharp
BettererFileTest.Create("MyFileTest", () =>
{
    var issues = new BettererFileIssues();
    issues.Add("src/Foo.cs", line: 10, column: 5, length: 3, message: "bad thing");
    return issues;
}, goal: BettererFileTest.NoIssues);
```

Each issue gets a stable, line-independent hash, so issues are tracked even as code moves; the
constraint is the total issue count (fewer is better).

## The results file

`.betterer.results` is a single JSON file you **commit**. Schema v2:

```json
{
  "version": 2,
  "results": {
    "InterfacesStartWithI": [ "MyApp.BadInterface" ],
    "OpenTodos": 3
  }
}
```

It is deterministic (sorted keys and values) and only rewritten when a result actually changes, so
diffs stay small. v1 files are read transparently.

## Seeding a baseline

A test that reports issues with **no baseline fails by default** — accepting a baseline is an
explicit choice. Record the current state with the `BETTERER_UPDATE` environment variable:

```bash
BETTERER_UPDATE=1 dotnet test          # xUnit
betterernet --config MyConfig.dll start  # the CLI records on first run too
```

`BETTERER_UPDATE` also accepts regressions (like betterer's `--update`). Commit the generated
`.betterer.results`; from then on, run normally.

## The CLI

Install the global tool:

```bash
dotnet tool install --global BettererNet.Cli   # run as `betterernet`
```

### Declarative config (`betterer.json`)

The simplest setup needs **no code** — a `betterer.json` describing the data-driven test types
(regex, coverage, SARIF, dotnet-format). `betterernet ci` auto-detects `betterer.json` in the working
directory (or pass `--config path/to/betterer.json`); relative paths resolve against the file.

```json
{
  "results": ".betterer.results",
  "tests": {
    "NoTodos":   { "type": "regex",    "pattern": "TODO", "includes": ["**/*.cs"], "ignoreCase": false },
    "Coverage":  { "type": "coverage", "report": "coverage.cobertura.xml", "goalZero": true },
    "Analyzers": { "type": "sarif",    "report": "analysis.sarif", "levels": ["error"] },
    "Format":    { "type": "format",   "report": "format-report.json", "diagnostics": ["WHITESPACE"] }
  }
}
```

Each test's key is its name; `goalZero: true` sets a goal of zero issues. `betterernet init`
scaffolds a starter `betterer.json`.

### Ownership & budgets

For large teams, any test can carry two extra keys (or, in code, be wrapped with
`.WithOwnership(owner, budget)`):

```json
{
  "tests": {
    "LegacyWarnings": {
      "type": "regex", "pattern": "#pragma warning disable", "includes": ["**/*.cs"],
      "owner": "@platform-team",
      "budget": 50
    }
  }
}
```

- **`owner`** — a person or team responsible for the test's debt. It's surfaced in the console
  reporter (`[worse] LegacyWarnings (owner: @platform-team)`) so a regression routes to whoever owns
  it, rather than the whole org.
- **`budget`** — a *hard ceiling* on the issue count, distinct from the ratchet and the goal. A run
  whose count exceeds the budget **fails the suite even if it improved on (or matched) its baseline**,
  and an over-budget result is never recorded — so a baseline can't be seeded above the ceiling. Set
  it at or above today's count to cap runaway growth while the ratchet drives the number down. (The
  budget is also folded into the `--cache` fingerprint, so lowering it re-runs the test.)

The ratchet says "no worse than last time"; the goal is the aspirational target; the budget is the
line you've decided must never be crossed.

### Compiled config (advanced)

Tests that need code — Roslyn syntax queries, NetArchTest rules, custom `BettererTest<T>` — come from
a **compiled config assembly**: a class library that implements `IBettererSuiteProvider`, passed with
`--config My.dll`:

```csharp
using System.Collections.Generic;
using BettererNet;

public sealed class BettererConfig : IBettererSuiteProvider
{
    public IEnumerable<IBettererTest> GetTests()
    {
        yield return BettererRegexTest.Create("NoTodos", "TODO", new[] { "**/*.cs" });
        yield return BettererCoverageTest.Create("Coverage", "coverage.cobertura.xml");
    }
}
```

Build it, then:

| Command | Behavior |
|---|---|
| `start` (default) | Run; record improvements; **fail** on a regression. |
| `ci` | Run **without writing**; fail if the results file is out of date or regressed. |
| `watch` | Run, then re-run on `.cs` changes. |
| `precommit` | Run, then `git add` the results file on success. |
| `results` | Print the current results file. |
| `init` | Scaffold a starter `BettererConfig.cs` (`--automerge` also sets up the git merge driver). |
| `merge` | Resolve a `.betterer.results` conflict (git merge-driver form). |

```bash
betterernet --config path/to/MyConfig.dll start
betterernet --config path/to/MyConfig.dll ci
```

Options:

| Option | Meaning |
|---|---|
| `--config <assembly>` / `-c` | The compiled config that supplies the tests. |
| `--results <path>` / `-r` | Results file path (default `.betterer.results`). |
| `--filter <regex>` / `-f` | Run only matching test names; repeatable; a leading `!` negates. |
| `--update` / `-u` | Accept regressions and record them. |
| `--workers <n>` / `-w` | Run up to *n* tests concurrently. |
| `--reporter <name>` / `-R` | `console` (default), `github`, or `silent` (case-insensitive). |
| `--sarif <path>` | Also write a SARIF 2.1.0 report of the current issues (composes with the reporter). |
| `--markdown <path>` | Also write a markdown run summary (verdict, per-test table, new issues) — post it as a PR comment from CI. |
| `--history <path>` | Append a per-test count snapshot to a history file and render a markdown trend (burn-down) beside it. |
| `--cache` / `--cache-path <path>` | Skip tests whose input files are unchanged, via a machine-local fingerprint cache (`.betterer.cache`; gitignore it). |
| `--silent` / `-s` | Suppress reporter output. |

Exit codes: `0` success, `1` a test failed / CI diff, `2` bad arguments or config load error.

## Merge & automerge

`.betterer.results` changes on many branches, so merges can conflict. `betterernet merge` does a
**tightest-baseline** merge — numbers take the minimum, sets/files take their intersection — so no
branch's improvements are lost. `betterernet init --automerge` writes a `.gitattributes` entry and
configures the git merge driver so conflicts resolve automatically:

```
.betterer.results merge=betterer
```

## Reporters & CI

- **console** (default): a per-test line plus a summary; on a regression it lists the specific new
  issues (`file:line message`).
- **github**: per-new-issue `::error file=,line=` annotations (so they land on the PR diff), a
  test-level error for failures, and a markdown table appended to `$GITHUB_STEP_SUMMARY` — use
  `--reporter github` in GitHub Actions.
- **silent**: no output.

Additionally, `--sarif <path>` writes a SARIF 2.1.0 report of the current issues alongside the chosen
reporter — upload it to GitHub Code Scanning, or re-import it with `BettererSarifTest`.

A minimal GitHub Actions step:

```yaml
- run: betterernet --config path/to/MyConfig.dll ci --reporter github
```

(For the xUnit workflow, just run `dotnet test`; failures appear as normal test failures.)

## FAQ

**Do I commit `.betterer.results`?** Yes — it is the shared baseline.

**A test fails on a fresh checkout with no baseline.** That's by design. Run once with
`BETTERER_UPDATE=1` (or `allowFirstFailure: true`) to record the baseline, then commit it.

**xUnit vs CLI — which do I use?** Either, or both: they share the same engine and results file.
xUnit fits teams already running `dotnet test`; the CLI fits dedicated `ci`/`watch`/`merge` flows.

**How do I temporarily run one test?** `--filter <regex>` on the CLI, or your normal xUnit filters.

See [ROADMAP.md](ROADMAP.md) for what's implemented and what's planned.
