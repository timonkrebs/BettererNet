using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace BettererNet.Cli;

/// <summary>The CLI commands, built on <see cref="BettererRunner"/>. Each returns a process exit code.</summary>
public static class BettererCli
{
    /// <summary>Parse arguments and dispatch to a command. Unknown options return a non-zero code.</summary>
    public static async Task<int> RunAsync(IReadOnlyList<string> args, IEnumerable<IBettererTest> tests, CancellationToken cancellationToken = default)
    {
        if (args.Count > 0 && args[0] == "merge")
        {
            return await MergeAsync(args, cancellationToken).ConfigureAwait(false);
        }

        if (args.Count > 0 && args[0] == "init")
        {
            return Init(Directory.GetCurrentDirectory(), args.Contains("--automerge"));
        }

        var (command, options, error) = Parse(args);
        if (error is not null)
        {
            Console.Error.WriteLine(error);
            return 2;
        }

        return command switch
        {
            "start" => await StartAsync(tests, options, cancellationToken).ConfigureAwait(false),
            "ci" => await CiAsync(tests, options, cancellationToken).ConfigureAwait(false),
            "precommit" => await PrecommitAsync(tests, options, cancellationToken).ConfigureAwait(false),
            "watch" => await WatchAsync(tests, options, cancellationToken).ConfigureAwait(false),
            "results" => await ResultsAsync(options, cancellationToken).ConfigureAwait(false),
            _ => Unknown(command),
        };
    }

    /// <summary>Run tests, record improvements, and fail if anything got worse.</summary>
    public static async Task<int> StartAsync(IEnumerable<IBettererTest> tests, BettererCliOptions options, CancellationToken cancellationToken = default)
    {
        var reporter = ResolveReporter(options);
        var selected = Filter(tests, options.Filters);
        var resultsFile = await BettererResultsFile.LoadAsync(options.ResultsPath, cancellationToken).ConfigureAwait(false);
        var context = new BettererRunContext { Update = options.Update };

        var summary = await BettererRunner
            .RunAsync(selected, resultsFile, context, write: true, maxDegreeOfParallelism: options.Workers, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        Report(reporter, summary);
        return summary.IsFailure ? 1 : 0;
    }

    /// <summary>Run tests without writing and fail if the results file is out of date or regressed.</summary>
    public static async Task<int> CiAsync(IEnumerable<IBettererTest> tests, BettererCliOptions options, CancellationToken cancellationToken = default)
    {
        var reporter = ResolveReporter(options);
        var selected = Filter(tests, options.Filters);
        var resultsFile = await BettererResultsFile.LoadAsync(options.ResultsPath, cancellationToken).ConfigureAwait(false);

        var summary = await BettererRunner
            .RunAsync(selected, resultsFile, new BettererRunContext(), write: false, maxDegreeOfParallelism: options.Workers, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        Report(reporter, summary);

        // CI fails if running would change the results file (a missing commit) or a test regressed.
        var hasDiff = summary.Runs.Any(run => run.Status is not (BettererRunStatus.Same or BettererRunStatus.Skipped));
        return hasDiff ? 1 : 0;
    }

    /// <summary>Run like <see cref="StartAsync"/>, then <c>git add</c> the results file when it passes.</summary>
    public static async Task<int> PrecommitAsync(IEnumerable<IBettererTest> tests, BettererCliOptions options, CancellationToken cancellationToken = default)
    {
        var code = await StartAsync(tests, options, cancellationToken).ConfigureAwait(false);
        if (code == 0 && File.Exists(options.ResultsPath))
        {
            TryGit($"add \"{options.ResultsPath}\"", Directory.GetCurrentDirectory());
        }

        return code;
    }

    /// <summary>Print the contents of the results file.</summary>
    public static async Task<int> ResultsAsync(BettererCliOptions options, CancellationToken cancellationToken = default)
    {
        var resultsFile = await BettererResultsFile.LoadAsync(options.ResultsPath, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"Results file: {Path.GetFullPath(options.ResultsPath)}");
        Console.WriteLine($"Tracked tests: {resultsFile.Results.Count}");
        foreach (var (name, value) in resultsFile.Results)
        {
            Console.WriteLine($"  - {name}: {Describe(value)}");
        }

        return 0;
    }

    /// <summary>Run once, then re-run whenever a <c>.cs</c> file under the current directory changes.</summary>
    public static async Task<int> WatchAsync(IEnumerable<IBettererTest> tests, BettererCliOptions options, CancellationToken cancellationToken = default)
    {
        var testList = tests.ToList();
        await StartAsync(testList, options, cancellationToken).ConfigureAwait(false);

        using var watcher = new FileSystemWatcher(Directory.GetCurrentDirectory())
        {
            IncludeSubdirectories = true,
            Filter = "*.cs",
            EnableRaisingEvents = true,
        };

        using var signal = new SemaphoreSlim(0);
        void OnChange(object _, FileSystemEventArgs __) => signal.Release();
        watcher.Changed += OnChange;
        watcher.Created += OnChange;
        watcher.Deleted += OnChange;
        watcher.Renamed += (_, _) => signal.Release();

        Console.WriteLine("Watching for changes (Ctrl+C to stop)...");
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await signal.WaitAsync(cancellationToken).ConfigureAwait(false);
                await Task.Delay(300, cancellationToken).ConfigureAwait(false); // debounce a burst of edits
                while (signal.CurrentCount > 0)
                {
                    await signal.WaitAsync(cancellationToken).ConfigureAwait(false);
                }

                await StartAsync(testList, options, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }

        return 0;
    }

    /// <summary>
    /// Resolve a results-file merge. Git-driver form: <c>merge &lt;base&gt; &lt;ours&gt; &lt;theirs&gt;</c>
    /// (writes to <c>ours</c>); manual form: <c>merge &lt;ours&gt; &lt;theirs&gt;</c>.
    /// </summary>
    public static async Task<int> MergeAsync(IReadOnlyList<string> args, CancellationToken cancellationToken = default)
    {
        var files = args.Skip(1).Where(arg => !arg.StartsWith('-')).ToList();
        string ours, theirs;
        if (files.Count >= 3)
        {
            (ours, theirs) = (files[1], files[2]); // base ours theirs
        }
        else if (files.Count == 2)
        {
            (ours, theirs) = (files[0], files[1]);
        }
        else
        {
            Console.Error.WriteLine("Usage: merge [<base>] <ours> <theirs>");
            return 2;
        }

        await BettererResultsMerge.MergeFilesAsync(ours, theirs, ours, cancellationToken).ConfigureAwait(false);
        return 0;
    }

    /// <summary>Scaffold a starter config and, with <paramref name="automerge"/>, configure the git merge driver.</summary>
    public static int Init(string directory, bool automerge = false)
    {
        var configPath = Path.Combine(directory, "BettererConfig.cs");
        if (File.Exists(configPath))
        {
            Console.WriteLine($"{configPath} already exists.");
        }
        else
        {
            File.WriteAllText(configPath, ConfigTemplate);
            Console.WriteLine($"Created {configPath}.");
        }

        if (automerge)
        {
            ConfigureAutomerge(directory);
            Console.WriteLine("Configured automerge for .betterer.results (.gitattributes + git merge driver).");
        }

        Console.WriteLine("Reference the BettererNet packages, build the config, then run: betterernet --config <assembly> ci");
        return 0;
    }

    /// <summary>Parse args into a command name and options. Returns an error message for bad input.</summary>
    public static (string Command, BettererCliOptions Options, string? Error) Parse(IReadOnlyList<string> args)
    {
        var command = "start";
        var commandSet = false;
        var results = BettererResultsFile.DefaultFileName;
        var filters = new List<string>();
        var workers = 1;
        string? reporter = null;
        bool update = false, strict = false, silent = false;

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "-r" or "--results":
                    if (++i >= args.Count)
                    {
                        return (command, new BettererCliOptions(), "Missing value for --results.");
                    }

                    results = args[i];
                    break;

                case "-f" or "--filter":
                    if (++i >= args.Count)
                    {
                        return (command, new BettererCliOptions(), "Missing value for --filter.");
                    }

                    filters.Add(args[i]);
                    break;

                case "-w" or "--workers":
                    if (++i >= args.Count || !int.TryParse(args[i], out workers) || workers < 1)
                    {
                        return (command, new BettererCliOptions(), "Invalid value for --workers (expected a positive integer).");
                    }

                    break;

                case "-u" or "--update":
                    update = true;
                    break;

                case "--strict":
                    strict = true;
                    break;

                case "-s" or "--silent":
                    silent = true;
                    break;

                case "-R" or "--reporter":
                    if (++i >= args.Count)
                    {
                        return (command, new BettererCliOptions(), "Missing value for --reporter.");
                    }

                    reporter = args[i];
                    if (reporter is not ("github" or "console" or "silent"))
                    {
                        return (command, new BettererCliOptions(), $"Unknown reporter '{reporter}'. Expected: console, github, silent.");
                    }

                    break;

                default:
                    if (arg.StartsWith('-'))
                    {
                        return (command, new BettererCliOptions(), $"Unknown option '{arg}'.");
                    }

                    if (commandSet)
                    {
                        return (command, new BettererCliOptions(), $"Unexpected argument '{arg}'.");
                    }

                    command = arg;
                    commandSet = true;
                    break;
            }
        }

        var options = new BettererCliOptions
        {
            ResultsPath = results,
            Filters = filters,
            Update = update,
            Strict = strict,
            Silent = silent,
            Workers = workers,
            ReporterName = reporter,
        };
        return (command, options, null);
    }

    // Filter patterns are user-provided; compile each once with a match timeout so a pathological
    // pattern can't hang the CLI via catastrophic backtracking (ReDoS).
    private static readonly TimeSpan FilterTimeout = TimeSpan.FromSeconds(1);

    private static IEnumerable<IBettererTest> Filter(IEnumerable<IBettererTest> tests, IReadOnlyList<string> filters)
    {
        if (filters.Count == 0)
        {
            return tests;
        }

        var positives = new List<Regex>();
        var negatives = new List<Regex>();
        foreach (var filter in filters)
        {
            if (filter.StartsWith('!'))
            {
                negatives.Add(new Regex(filter[1..], RegexOptions.None, FilterTimeout));
            }
            else
            {
                positives.Add(new Regex(filter, RegexOptions.None, FilterTimeout));
            }
        }

        return tests.Where(test =>
            !negatives.Any(pattern => pattern.IsMatch(test.Name)) &&
            (positives.Count == 0 || positives.Any(pattern => pattern.IsMatch(test.Name))));
    }

    private static IBettererReporter ResolveReporter(BettererCliOptions options)
    {
        if (options.Reporter is not null)
        {
            return options.Reporter;
        }

        if (options.Silent)
        {
            return new BettererSilentReporter();
        }

        return options.ReporterName?.ToLowerInvariant() switch
        {
            "github" => new BettererGitHubActionsReporter(),
            "silent" => new BettererSilentReporter(),
            _ => new BettererConsoleReporter(),
        };
    }

    private static void Report(IBettererReporter reporter, BettererSuiteSummary summary)
    {
        foreach (var run in summary.Runs)
        {
            reporter.ReportRun(run);
        }

        reporter.ReportSuite(summary);
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'. Expected: init, start, ci, watch, precommit, results, merge.");
        return 2;
    }

    private static string Describe(JsonNode value) => value switch
    {
        JsonArray array => $"{array.Count} issue(s)",
        JsonObject obj => $"{obj.Sum(pair => pair.Value is JsonArray array ? array.Count : 0)} issue(s) across {obj.Count} file(s)",
        _ => value.ToJsonString(),
    };

    private static void ConfigureAutomerge(string directory)
    {
        var attributesPath = Path.Combine(directory, ".gitattributes");
        const string entry = ".betterer.results merge=betterer";
        var existing = File.Exists(attributesPath) ? File.ReadAllText(attributesPath) : string.Empty;
        if (!existing.Contains(entry, StringComparison.Ordinal))
        {
            var prefix = existing.Length > 0 && !existing.EndsWith('\n') ? "\n" : string.Empty;
            File.AppendAllText(attributesPath, $"{prefix}{entry}\n");
        }

        TryGit("config merge.betterer.driver \"betterernet merge %O %A %B\"", directory);
    }

    private const int GitTimeoutMilliseconds = 10_000;

    private static void TryGit(string arguments, string workingDirectory)
    {
        try
        {
            var startInfo = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
            };
            startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0"; // never block on an interactive prompt

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return;
            }

            if (!process.WaitForExit(GitTimeoutMilliseconds))
            {
                process.Kill(entireProcessTree: true); // best effort: don't let a stuck git hang us
            }
        }
        catch
        {
            // Best effort: commands still succeed even if git is missing, slow, or blocked.
        }
    }

    private const string ConfigTemplate = """
        using System.Collections.Generic;
        using BettererNet;

        // Build this into an assembly, then run: betterernet --config <assembly>.dll ci
        public sealed class BettererConfig : IBettererSuiteProvider
        {
            public IEnumerable<IBettererTest> GetTests()
            {
                yield return BettererRegexTest.Create("NoTodos", "TODO", new[] { "**/*.cs" });
            }
        }
        """;
}
