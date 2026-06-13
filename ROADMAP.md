# BettererNet Roadmap

Plan to reach feature parity with [`betterer`](https://github.com/phenomnomnominal/betterer)
and to add `.NET`-native capabilities on top.

> **Parity is not a literal port.** `betterer` lives in the JS/TS/lint/AST world.
> The equivalent capabilities in .NET come from Roslyn, analyzers, `.editorconfig`,
> coverlet and friends. The "what to build" sections below map each `betterer`
> concept onto its idiomatic .NET counterpart.

> **Progress:** ✅ Phase 0 (Foundation) complete. ▶️ Phase 1 (core engine) is next.
> Section 1 below describes the pre-Phase-0 baseline for the gap analysis.

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

`betterer` is CLI-first; BettererNet's current xUnit-assertion style is a genuinely nice .NET
ergonomic. We keep both by building a rich **core library** and exposing it through two
front-ends that share it:

```
BettererNet.Core          # engine: tests, constraints, goals, results file, state machine
  ├── BettererNet.Cli      # `dotnet betterernet` tool: init/start/ci/watch/precommit/results/merge
  ├── BettererNet.Xunit    # xUnit (+ NUnit/MSTest later) adapter — keeps `dotnet test` ergonomics
  └── integrations/
        ├── BettererNet.Regex       # regex match-count test
        ├── BettererNet.Roslyn      # analyzer + compiler/nullable + syntax-query tests
        ├── BettererNet.Coverage    # coverlet / Cobertura coverage test
        └── BettererNet.NetArchTest # architecture-test wrapper
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
- ✅ Retargeted to `net8.0` (LTS) via a root `Directory.Build.props`; dropped EOL `net5.0`.
- ✅ Restructured to `src/` (`BettererNet.Core` / `.Xunit` / `.Cli`), `samples/`, `tools/`,
  `tests/`, with a solution at the repo root. (`integrations/*` land in Phase 2.)
- ✅ Single `.betterer.results` reader/writer (`BettererResultsFile`) — deterministic,
  sorted, indented, atomic writes; diff-stable across runs.
- ✅ Migrated the xUnit adapter onto the single results file; added a `BETTERER_UPDATE`
  seeding escape hatch and a `tests/BettererNet.Tests` suite covering core + adapter.

### Phase 1 — Core engine (parity backbone) ⭐ highest leverage
- `BettererTest<T>` with `Test` / `Constraint` / `Goal` / `Deadline`.
- Constraints (`Bigger` / `Smaller` / custom) and the full result-state machine
  (`new/better/same/worse/complete/updated/skipped/failed/expired`).
- `BettererFileTest` with per-file issues and **file-hash + issue-hash** tracking.
- `RunSummary` / `SuiteSummary`; refactor the existing xUnit path onto the engine.

> Every downstream capability depends on this abstraction + the hashed single results file,
> which BettererNet entirely lacks today. Built-in tests and CLI modes are comparatively
> mechanical once this exists.

### Phase 2 — Built-in tests
Order by effort/value: Regex → Roslyn analyzer → Roslyn compiler/nullable → Roslyn syntax-query
→ Coverage → NetArchTest wrapper.

### Phase 3 — CLI & modes
`dotnet tool` with `init` / `start` / `ci` / `watch` / `precommit` / `results`; `--filter`,
`--exclude`/`--ignore`, `--update`, `--strict`; `IBettererReporter` + default/CI/silent reporters.

### Phase 4 — Merge & cache
`merge` command + automerge git driver (`init --automerge`); `--cache`/`--cachePath`
change-detection; `--workers` parallelism.

### Phase 5 — Value-adds (see §6).

---

## 6. Features worth adding *on top* of parity (.NET-native)

1. **MSBuild / `dotnet build` task** — run betterer in the build; fail on regression with no separate step.
2. **Roslyn syntax-query test as a headline feature** — .NET's answer to `tsquery` is genuinely more powerful than the JS original; lean into it.
3. **Nullable-reference-type adoption preset** — turnkey "incrementally enable `#nullable`" recipe (the canonical .NET migration pain point).
4. **SARIF import/export** — ingest any analyzer that emits SARIF (huge ecosystem) and export for GitHub Code Scanning.
5. **GitHub Actions / Azure DevOps reporters** — PR annotations and step summaries out of the box.
6. **`dotnet format` / EditorConfig integration** — incrementally tighten style rules.
7. **Per-test ownership & budgets** — assign owners/teams to debt and per-area budgets (serves the "large teams" promise).
8. **HTML/markdown trend report** — visualize debt burning down over time.
9. **Solution-wide discovery** — point at a `.sln`, auto-enumerate projects (the InspectCode app already gestures at this).

---

## 7. Recommended next step

Phase 0 is done: the solution is on `net8.0`, restructured into `src`/`samples`/`tools`/`tests`,
and the single diff-stable `.betterer.results` reader/writer is in place and under test.

**Phase 1 (core engine)** is next. It is the dependency root for parity: introduce
`BettererTest<T>` with constraints/goals/deadlines, the full result-state machine, and
`BettererFileTest` with per-file issue hashing, then move the xUnit adapter onto that engine.
