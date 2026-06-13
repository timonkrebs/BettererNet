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

        if (JsonNode.Parse(File.ReadAllText(fullPath)) is not JsonObject root)
        {
            throw new InvalidOperationException($"'{path}' is not a JSON object.");
        }

        var results = root["results"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(results))
        {
            results = ResolvePath(results, baseDirectory);
        }

        var tests = new List<IBettererTest>();
        if (root["tests"] is JsonObject testsObject)
        {
            foreach (var (name, node) in testsObject)
            {
                if (node is JsonObject spec)
                {
                    tests.Add(Build(name, spec, baseDirectory));
                }
            }
        }

        return (tests, results);
    }

    private static IBettererTest Build(string name, JsonObject spec, string baseDirectory)
    {
        var type = spec["type"]?.GetValue<string>()?.ToLowerInvariant()
            ?? throw new InvalidOperationException($"Test '{name}' is missing a 'type'.");

        var goal = (spec["goalZero"]?.GetValue<bool>() ?? false) ? BettererFileTest.NoIssues : null;

        return type switch
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
