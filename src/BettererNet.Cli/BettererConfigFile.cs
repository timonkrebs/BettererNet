using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace BettererNet.Cli;

/// <summary>
/// Loads a declarative <c>betterer.json</c> so the built-in data-driven tests (regex, coverage,
/// SARIF) can run without compiling a C# config assembly. Relative paths resolve against the file's
/// directory. Tests needing code (Roslyn syntax queries, NetArchTest rules) use a compiled config.
/// </summary>
public static class BettererConfigFile
{
    /// <summary>Whether <paramref name="path"/> looks like a declarative config rather than an assembly.</summary>
    public static bool IsConfigFile(string path) =>
        path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".jsonc", StringComparison.OrdinalIgnoreCase);

    /// <summary>Parse the config into tests and an optional results-file path.</summary>
    public static (IReadOnlyList<IBettererTest> Tests, string? ResultsPath) Load(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var baseDirectory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();

        // Tolerate comments and trailing commas so `.json` and `.jsonc` both parse.
        var documentOptions = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        if (JsonNode.Parse(File.ReadAllText(fullPath), documentOptions: documentOptions) is not JsonObject root)
        {
            throw new InvalidOperationException($"'{path}' is not a JSON object.");
        }

        var results = root["results"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(results))
        {
            results = ResolvePath(results, baseDirectory);
        }

        var tests = new List<IBettererTest>();
        if (root["tests"] is { } testsNode)
        {
            if (testsNode is not JsonObject testsObject)
            {
                throw new InvalidOperationException("'tests' must be a JSON object of name -> test.");
            }

            foreach (var (name, node) in testsObject)
            {
                if (node is not JsonObject spec)
                {
                    throw new InvalidOperationException($"Test '{name}' must be a JSON object.");
                }

                tests.Add(Build(name, spec, baseDirectory));
            }
        }

        return (tests, results);
    }

    private static IBettererTest Build(string name, JsonObject spec, string baseDirectory)
    {
        var type = spec["type"]?.GetValue<string>()?.ToLowerInvariant()
            ?? throw new InvalidOperationException($"Test '{name}' is missing a 'type'.");

        var goal = (spec["goalZero"]?.GetValue<bool>() ?? false) ? BettererFileTest.NoIssues : null;

        IBettererTest test = type switch
        {
            "regex" => BettererRegexTest.Create(
                name,
                Required(spec, name, "pattern"),
                ReadStringList(spec["includes"]) ?? new[] { "**/*.cs" },
                baseDirectory: ReadOptionalDirectory(spec["baseDirectory"], baseDirectory),
                options: (spec["ignoreCase"]?.GetValue<bool>() ?? false) ? RegexOptions.IgnoreCase : RegexOptions.None,
                goal: goal),

            "coverage" => BettererCoverageTest.Create(
                name,
                ResolvePath(Required(spec, name, "report"), baseDirectory),
                goal: goal),

            "sarif" => BettererSarifTest.Create(
                name,
                ResolvePath(Required(spec, name, "report"), baseDirectory),
                levels: ReadStringSet(spec["levels"]),
                goal: goal),

            _ => throw new InvalidOperationException(
                $"Test '{name}' has unknown type '{type}'. Supported declarative types: regex, coverage, sarif."),
        };

        // Optional large-team triage metadata: an owner to route debt to, and a hard issue ceiling.
        var owner = spec["owner"]?.GetValue<string>();
        var budget = spec["budget"] is JsonValue budgetNode && budgetNode.TryGetValue<int>(out var budgetValue)
            ? budgetValue
            : (int?)null;

        return test.WithOwnership(owner, budget);
    }

    private static string Required(JsonObject spec, string name, string field) =>
        spec[field]?.GetValue<string>() ?? throw new InvalidOperationException($"Test '{name}' is missing '{field}'.");

    private static IReadOnlyList<string>? ReadStringList(JsonNode? node) =>
        node is JsonArray array ? array.Select(item => item!.GetValue<string>()).ToList() : null;

    private static ISet<string>? ReadStringSet(JsonNode? node) =>
        node is JsonArray array
            ? new HashSet<string>(array.Select(item => item!.GetValue<string>()), StringComparer.OrdinalIgnoreCase)
            : null;

    private static string ResolvePath(string path, string baseDirectory) =>
        Path.IsPathRooted(path) ? path : Path.Combine(baseDirectory, path);

    private static string ReadOptionalDirectory(JsonNode? node, string baseDirectory)
    {
        var value = node?.GetValue<string>();
        return string.IsNullOrEmpty(value) ? baseDirectory : ResolvePath(value, baseDirectory);
    }
}
