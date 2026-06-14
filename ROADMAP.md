# BettererNet Roadmap

Plan to reach feature parity with [`betterer`](https://github.com/phenomnomnominal/betterer)
and to add `.NET`-native capabilities on top.

> **Parity is not a literal port.** `betterer` lives in the JS/TS/lint/AST world.
> The equivalent capabilities in .NET come from Roslyn, analyzers, `.editorconfig`,
> coverlet and friends. The "what to build" sections below map each `betterer`
> concept onto its idiomatic .NET counterpart.

> **Progress:** ✅ Phases 0-4 complete and Phase 5 underway — the engine, the xUnit adapter, the
> built-in integrations (now including SARIF import), the `betterernet` CLI with
> merge/automerge/`--workers`, a GitHub Actions reporter, **NuGet packaging + the global tool, and a
> declarative `betterer.json`, diff-surfacing reporters, SARIF export, NUnit + MSTest adapters, and
> MSBuild-workspace loading, content-based hashing + `--cache`, a PR-comment reporter (`--markdown`),
> a trend/history report (`--history`), a nullable-adoption preset, and per-test ownership & budgets**
> are in place. ▶️ Remaining Phase 6 (lower priority): a TUnit adapter (see §5). Section 1 below
> describes the pre-Phase-0 baseline.

---

## 1. Where BettererNet is today

The repo is an early prototype with **two disconnected approaches** and no shared core:

| Component | What it does | Limitations |
|---|---|---|
| `BettererNet/Betterer.cs` | xUnit helper. `AssertAsync(BettererResult, allowFirstFailure)` stores one JSON file per test method (`BettererResults/{MethodName}.json`), each holding a `List<string> FailingTypeNames`. Fails only if a **new** name appears vs. the baseline; deletes the file when clean. | Bare strings only — no file/line/message, no hashing, per-method files, binary pass/fail, no numeric tracking, no goals. |
| `BettererResult.cs` | `{ DateTimeOffset DateTime; List<string> FailingTypeNames }` | Minimal data model. |
| `InspectCodeSnapshot/` | Standalone console app: runs ReSharper `InspectCode`, serializes XML, diffs new vs. snapshot per file/violation, exits 1 on new violations. | Separate from the xUnit path, ReSharper-only, naive file-count diff, no shared abstractions. |
| `SampleTest/` | Demo NetArchTest rules → `BettererResult`. | Samples only. |

Targets `net5.0` (EOL). Net-net: BettererNet today is *"snapshot a list of named failures and fail on regressions"* — roughly **10–15%** of `betterer`'s surface.

## 2. What `betterer` is (so the gap is precise)

`betterer` is a **standalone CLI test runner** driven by a config file (`.betterer.ts`). Core model:

- **`BettererTest<D,S,Diff>`** = a `test` function producing a value + a **`constraint`** (compares new vs. baseline → better/same/worse) + optional **`goal`** + optional **`deadline`**.
- **`BettererFileTest`** = file-oriented test tracking **issues per file** (line, column, length, message) with **file-hash + issue-hash** so issues survive code moving around.
- **Counting tests** via `bigger`/`smaller` constraints.
- **One results file** (`.betterer.results`) with hashed entries.
- **Result states**: `new`, `better`, `same`, `worse`, `complete` (goal met), `updated`, `skipped`, `failed`, `expired`.
- **CLI**: `init`, `start` (default), `ci`, `watch`, `precommit`, `results`, `merge`, `upgrade` — with `--config --results --filter --exclude --ignore --reporter --silent --strict --update --cache --cachePath --workers --tsconfig --automerge`.
- **Built-in integrations**: `typescript`, `eslint`, `regexp`, `tsquery`, `stylelint`, `coverage`.
- **Merge handling**: `betterer merge` + a git automerge driver for the results file.
- Pluggable **reporters**, **VSCode extension**.

---

## 3. Architecture: hybrid (shared core + CLI + xUnit adapter)

`betterer` is CLI-first; BettererNet's current xUnit-assertion style is genuinely nice .NET
ergonomics. We keep both by building a rich **core library** and exposing it through two
front-ends that share it:

```
BettererNet.Core          # engine: tests, constraints, goals, results file, state machine
  ├── BettererNet.Cli      # `dotnet betterernet` tool: init/start/ci/watch/precommit/results/merge
  ├── BettererNet.Xunit    # xUnit / NUnit / MSTest adapters — keep `dotnet test` ergonomics
  └── integrations/
        ├── BettererNet.Regex          # regex match-count test
        ├── BettererNet.Roslyn(.MSBuild) # analyzer + compiler/nullable + syntax-query / whole-project tests
        ├── BettererNet.Coverage       # coverlet / Cobertura coverage test
        ├── BettererNet.NetArchTest    # architecture-test wrapper
        ├── BettererNet.Sarif          # import any SARIF report
        └── BettererNet.Format         # dotnet format report test
```

- **CLI** drives true parity: `ci` / `watch` / `merge` / `precommit` / reporters, configured by a
  **compiled C# config assembly** — the direct analog of `betterer`'s compiled `.betterer.ts`.
- **xUnit adapter** keeps the in-test assertion flow that exists today, now backed by the same
  engine, file tests, goals, and shared results file.

---

## 4. Gap analysis & .NET mapping

### Core engine

| `betterer` concept | BettererNet today | What to build (.NET) |
|---|---|---|
| Single `.betterer.results` with hashes | ❌ per-method JSON, bare strings | One results file; entries keyed by test name; **file-hash + issue-hash** so issues track through edits/moves |
| `BettererTest` (test + constraint + goal + deadline) | ❌ fixed list-diff only | Generic `BettererTest<T>` with `Test`, `Constraint`, `Goal`, `Deadline` |
| `BettererFileTest` (per-file issues) | ❌ type-name strings only | `BettererFileTest` with `FileIssue { File, Line, Column, Length, Message, Hash }` |
| Counting/numeric tests (`bigger`/`smaller`) | ❌ | Numeric test + `Bigger`/`Smaller` constraints |
| Result states | ⚠️ pass/fail + new-baseline only | Full state machine + per-run `RunSummary` / `SuiteSummary` |
| Goals & deadlines | ❌ | `Goal` predicate + `Deadline` date (mark `complete`, fail when `expired`) |

### Built-in test integrations (the high-value .NET analogs)

| `betterer` test | .NET analog to build |
|---|---|
| `typescript` (adopt strict TS incrementally) | **Roslyn compiler-diagnostics test** — incrementally adopt **nullable reference types** / warnings-as-errors, tracked per file |
| `eslint` | **Roslyn analyzer test** — run analyzers / `.editorconfig` rules (`CSharpCompilation` / `dotnet format`), track diagnostic IDs |
| `tsquery` (query the AST) | **Roslyn syntax/semantic-query test** — query the C# syntax tree (the natural, more powerful .NET equivalent) |
| `regexp` | **Regex test** — count regex matches across globbed files (near-direct port, easiest first win) |
| `coverage` | **Coverage test** — parse coverlet/Cobertura, fail when coverage drops / track uncovered lines per file |
| `NetArchTest` (already in samples) | First-class **architecture-test** wrapper |
| `stylelint` | Skip (low .NET relevance) or optional CSS-linter shim |

### CLI, modes & ergonomics

| `betterer` | BettererNet today | What to build |
|---|---|---|
| `start` (default; update results when better) | ⚠️ xUnit assert auto-updates baseline | `betterernet` `dotnet tool` default run |
| `ci` (fail on **any** diff, incl. uncommitted improvement) | ⚠️ InspectCode CLI is CI-ish only | `ci` mode on unified engine |
| `watch` (re-run on file change) | ❌ | `FileSystemWatcher`-based `watch` |
| `precommit` (`git add` results if better, throw if worse) | ❌ | `precommit` mode |
| `results` (print summary) | ❌ | `results` command |
| `merge` + git automerge driver | ❌ | `merge` command + `git config` merge driver; `init --automerge` |
| `init` | ❌ | scaffold config + results file |
| `--filter` (subset of tests) | ❌ | regex test filter |
| `--exclude` / `--ignore` (files) | ⚠️ violation filter only | glob/regex file filters |
| `--update` / `--strict` | ⚠️ implicit update; no strict | explicit `--update` + `--strict` |
| `--cache` / `--cachePath` (only changed files) | ❌ | file-hash cache to skip unchanged files |
| `--workers` (parallelism) | ❌ | parallel test execution |
| `--reporter` / `--silent` (pluggable) | ❌ Console only | `IBettererReporter` + default/CI/silent reporters |
| `--config` (config file) | ❌ hardcoded | compiled **C# config assembly** (analog of compiled `.betterer.ts`) and/or JSON |

---

## 5. Phased roadmap

### Phase 0 — Foundation ✅ complete
- ✅ Retargeted to `net10.0` (LTS) via a root `Directory.Build.props`; dropped EOL `net5.0`.
- ✅ Restructured to `src/` (`BettererNet.Core` / `.Xunit` / `.Cli`), `samples/`, `tools/`,
  `tests/`, with a solution at the repo root. (`integrations/*` land in Phase 2.)
- ✅ Single `.betterer.results` reader/writer (`BettererResultsFile`) — deterministic,
  sorted, indented, atomic writes; diff-stable across runs.
- ✅ Migrated the xUnit adapter onto the single results file; added a `BETTERER_UPDATE`
  seeding escape hatch and a `tests/BettererNet.Tests` suite (19 tests covering the core,
  the adapter ratchet/isolation semantics, and concurrent writes).

### Phase 1 — Core engine (parity backbone) ⭐ highest leverage ✅ complete
- ✅ `BettererTest<T>` with `Test` / `Constraint` / `Goal` / `Deadline`.
- ✅ Constraints (`Bigger` / `Smaller` / `SetBased`) and the full result-state machine
  (`new/better/same/worse/complete/updated/skipped/failed/expired`).
- ✅ `BettererRunSummary` / `BettererSuiteSummary` + `BettererRunner`; the xUnit adapter is
  refactored onto the engine and now runs any `IBettererTest`; counting tests via `BettererCountTest`.
- ✅ Results file generalised to store any serialized value (schema v2, canonical & diff-stable,
  with a v1 read shim).
- ✅ `BettererFileTest` with per-file issues (line/column/length/message) and a stable, line-
  independent **issue-hash**, plus a hash-based file diff (`BettererFileIssues.Diff`).
  (Content-based **file-hash** tracking lands with the Phase 2 Roslyn integration that reads files.)

> Every downstream capability depends on this abstraction + the hashed single results file,
> which BettererNet entirely lacks today. Built-in tests and CLI modes are comparatively
> mechanical once this exists.

### Phase 2 — Built-in tests ✅ complete
- ✅ `BettererRegexTest` (`BettererNet.Regex`) — count regex matches across globbed files.
- ✅ `BettererRoslynTest` (`BettererNet.Roslyn`) — compiler diagnostics (nullable/typescript
  analog), analyzers (eslint analog), and syntax queries (tsquery analog) over C# source.
- ✅ `BettererCoverageTest` (`BettererNet.Coverage`) — track uncovered lines from a Cobertura report.
- ✅ `BettererArchTest` (`BettererNet.NetArchTest`) — wrap a NetArchTest rule as a first-class test.
- ☐ (Refinement, deferred) MSBuild-workspace loading + content-based file hashing for the Roslyn
  tests — they currently operate on explicit source paths / a `Compilation`.

### Phase 3 — CLI & modes ✅ complete
- ✅ `betterernet` `dotnet tool` with `init` / `start` / `ci` / `watch` / `precommit` / `results`,
  loading tests from a compiled config assembly (`IBettererSuiteProvider`) via a plugin
  `AssemblyLoadContext` (verified end-to-end).
- ✅ `--filter` (regex, negatable with `!`), `--results`, `--update`, `--silent`; `IBettererReporter`
  with default console and silent reporters.
- ☐ (Minor, deferred) `--strict` is parsed but currently a no-op; `--reporter <package>` (custom
  reporter loaded by name) and richer `watch` ergonomics.

### Phase 4 — Merge & cache — mostly complete
- ✅ `BettererResultsMerge` + the `merge` command (git-driver form) — tightest-baseline merge of
  `.betterer.results` (numbers→min, arrays/objects→intersection); `init --automerge` writes the
  `.gitattributes` entry and configures the git merge driver.
- ✅ `--workers` parallelism — `BettererRunner` runs tests concurrently and applies writes serially.
- ☐ `--cache` / `--cachePath` incremental runs — needs a small per-file API so file tests can skip
  unchanged files (deferred).

### Phase 5 — Value-adds — in progress
- ✅ **SARIF import** (`BettererNet.Sarif`) — turn any SARIF analyzer report into a file test,
  unlocking the whole SARIF-emitting ecosystem (Roslyn analyzers, `dotnet format`, etc.).
- ✅ **GitHub Actions reporter** — `::error` annotations + a `$GITHUB_STEP_SUMMARY` table, selected
  with `--reporter github` (which also wired up the `--reporter` flag).
- ☐ MSBuild task, reporters for other CI systems (see §6).

### Phase 6 — Adoption & insights (next) — prioritized
At functional parity, the highest-value work is making BettererNet *adoptable* and its output
*actionable* rather than adding more parity. Tiers are roughly highest-value-first.

**Tier 1 — adoption blockers ✅ done:**
- ✅ **NuGet packages + global tool** — all eight projects pack with shared metadata; the CLI ships as
  `dotnet tool install -g BettererNet.Cli` (run `betterernet`) and bundles the data-driven
  integrations. (A dedicated `dotnet new` template is still nice-to-have; `betterernet init` scaffolds
  a `betterer.json` for now.)
- ✅ **Declarative `betterer.json`** — run the data-driven tests (regex, coverage, SARIF) with no
  compiled config; `betterernet ci` auto-detects the file. (Roslyn/NetArchTest still use a compiled config.)

**Tier 2 — make the output actionable:**
- ✅ **Surface the diff in reporters** — the console and GitHub reporters now list the specific new
  issues on a regression (console `file:line message`; GitHub per-issue `::error file=,line=` annotations).
- ✅ **PR-comment reporter** — `--markdown <path>` writes a run summary (verdict, per-test table, new
  issues) to post as a PR comment from CI (`gh pr comment --body-file` or a sticky-comment action).
- ✅ **Trend report / history** — `--history <path>` appends a per-test count snapshot each run and
  renders a markdown burn-down beside it.

**Tier 3 — depth & performance:**
- ✅ **MSBuild-workspace loading** (`BettererNet.Roslyn.MSBuild` → `BettererProjectTest.FromProject` /
  `FromSolution`) — analyse a real `.csproj`/`.sln` with its actual references, build options, and full
  source set via `MSBuildWorkspace` (needs the .NET SDK at runtime). Content-based hashing + `--cache`
  can build on this next.
- ✅ **`--cache` / `--cache-path`** — content-based file fingerprints (`BettererFileFingerprint` +
  `BettererCache`) let the runner skip tests whose inputs are unchanged; biggest win on the slow
  Roslyn/MSBuild tests. The cache is machine-local (gitignored).
- ✅ **Nullable-adoption preset** — `BettererNullableTest.Create(name, projectPath)`
  (`BettererNet.Roslyn.MSBuild`) wraps `BettererProjectTest.FromProject` filtered to the nullable
  warnings (`CS8600`+), with a goal of zero: enable `<Nullable>enable</Nullable>`, baseline, burn down.
- ✅ **`dotnet format` integration** — `BettererFormatTest` (`BettererNet.Format`) ingests a
  `dotnet format --verify-no-changes --report` JSON report (whitespace, imports, analyzer/style fixes),
  with an optional `DiagnosticId` filter; also a declarative `"type": "format"`. Adopt a strict
  `.editorconfig` one rule at a time.
- ✅ **SARIF export** — `--sarif <path>` writes a SARIF 2.1.0 report of the current issues (composes
  with any reporter; round-trips through SARIF import; feeds GitHub Code Scanning).

**Tier 4 — ecosystem & robustness:**
- ◐ **More test-framework adapters** — ✅ NUnit (`BettererNet.NUnit`) and MSTest
  (`BettererNet.MSTest`), thin shims over the framework-agnostic `BettererAssertions`; TUnit remains.
- ✅ **Per-test ownership & budgets** — `IBettererTest.WithOwnership(owner, budget)` (and `owner` /
  `budget` keys in `betterer.json`) tag any test with a responsible person/team (surfaced by the
  console reporter) and a hard issue ceiling that fails the run when crossed — even on an improvement —
  and is never recorded above, so debt routes to its owner and can't quietly balloon.
- ☐ **Cross-process-safe results file** — the xUnit adapter's lock is process-wide, so parallel
  test *projects* could still race.

---

## 6. Features worth adding *on top* of parity (.NET-native)

1. **MSBuild / `dotnet build` task** — run BettererNet in the build; fail on regression with no separate step.
2. **Roslyn syntax-query test as a headline feature** — .NET's answer to `tsquery` is genuinely more powerful than the JS original; lean into it.
3. **Nullable-reference-type adoption preset** — turnkey "incrementally enable `#nullable`" recipe (the canonical .NET migration pain point).
4. **SARIF import/export** — ingest any analyzer that emits SARIF (huge ecosystem) and export for GitHub Code Scanning.
5. **GitHub Actions / Azure DevOps reporters** — PR annotations and step summaries out of the box.
6. ✅ **`dotnet format` integration** — `BettererFormatTest` ingests a `dotnet format` report to incrementally tighten style rules.
7. ✅ **Per-test ownership & budgets** — assign owners/teams to debt and a hard budget per test (serves the "large teams" promise).
8. **HTML/markdown trend report** — visualize debt burning down over time.
9. **Solution-wide discovery** — point at a `.sln`, auto-enumerate projects (the InspectCode app already gestures at this).

---

## 7. Recommended next step

Phases 0-4 are complete and Phase 5 is partly done: the engine, the xUnit adapter, the built-in
integrations (Regex; Roslyn; coverage; NetArchTest; **SARIF import**), the `betterernet` CLI with
merge/automerge/`--workers`, and a **GitHub Actions reporter** — all covered end-to-end by the test suite.

**Phase 6 Tier 1 (adoption) is done** — NuGet packages, the `betterernet` global tool, and a
declarative `betterer.json`, verified end-to-end (packed, installed, ran a config with no compiled
assembly).

Done so far in Phase 6: Tier 1 (packaging, global tool, declarative `betterer.json`); diff-surfacing
reporters; SARIF export; NUnit and MSTest adapters; MSBuild-workspace loading; content-based hashing +
`--cache`; a **PR-comment reporter** (`--markdown`); a **trend/history report** (`--history`); a
**nullable-adoption preset** (`BettererNullableTest`); **per-test ownership & budgets**
(`WithOwnership` / `owner` + `budget`); and a **`dotnet format` integration** (`BettererFormatTest`).

Remaining (lower priority): a TUnit adapter.
