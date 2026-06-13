using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BettererNet;

/// <summary>
/// Reader/writer for the single <c>.betterer.results</c> baseline file. Each test's serialized
/// result is stored under its name; values are kept canonical (see <see cref="JsonCanonicalizer"/>)
/// so the file is deterministic and diff-stable, and writes are atomic.
/// </summary>
public sealed class BettererResultsFile
{
    /// <summary>The conventional file name for a Betterer results file.</summary>
    public const string DefaultFileName = ".betterer.results";

    private const int CurrentVersion = 2;

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        // Results files are read and merged by humans; keep `+`, backticks etc. unescaped.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly SortedDictionary<string, JsonNode> _results;

    private BettererResultsFile(string path, SortedDictionary<string, JsonNode> results)
    {
        Path = path;
        _results = results;
    }

    /// <summary>Path of the results file on disk.</summary>
    public string Path { get; }

    /// <summary>The stored results, keyed by test name (ordinal-sorted). Values are canonical.</summary>
    public IReadOnlyDictionary<string, JsonNode> Results => _results;

    /// <summary>Load the results file, returning an empty instance if it does not yet exist.</summary>
    public static async Task<BettererResultsFile> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var results = new SortedDictionary<string, JsonNode>(StringComparer.Ordinal);

        if (File.Exists(path))
        {
            var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            if (JsonNode.Parse(text) is JsonObject root)
            {
                var version = root["version"]?.GetValue<int>() ?? 1;
                if (root["results"] is JsonObject stored)
                {
                    foreach (var (name, node) in stored)
                    {
                        if (node is null)
                        {
                            continue;
                        }

                        // v1 stored an object { timestamp, issues }; the value is the issues array.
                        var value = version < 2 && node is JsonObject legacy && legacy["issues"] is JsonArray issues
                            ? issues
                            : node;

                        if (JsonCanonicalizer.Canonicalize(value) is { } canonical)
                        {
                            results[name] = canonical;
                        }
                    }
                }
            }
        }

        return new BettererResultsFile(path, results);
    }

    /// <summary>Get the stored (canonical) result for a test, if one exists.</summary>
    public bool TryGet(string name, [NotNullWhen(true)] out JsonNode? value)
    {
        if (_results.TryGetValue(name, out var node))
        {
            value = node;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Store a test's result (canonicalized). A <c>null</c> value removes the entry.
    /// Returns <c>true</c> if the stored content actually changed.
    /// </summary>
    public bool Set(string name, JsonNode? value)
    {
        if (value is null)
        {
            return Remove(name);
        }

        var canonical = JsonCanonicalizer.Canonicalize(value)!;
        if (_results.TryGetValue(name, out var existing) && JsonCanonicalizer.AreEqual(existing, canonical))
        {
            return false;
        }

        _results[name] = canonical;
        return true;
    }

    /// <summary>Remove a test's result. Returns <c>true</c> if one was removed.</summary>
    public bool Remove(string name) => _results.Remove(name);

    /// <summary>Persist the results to disk atomically. An empty set deletes the file.</summary>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (_results.Count == 0)
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }

            return;
        }

        var results = new JsonObject();
        foreach (var (name, value) in _results)
        {
            results[name] = value.DeepClone();
        }

        var root = new JsonObject
        {
            ["version"] = CurrentVersion,
            ["results"] = results,
        };

        var tempPath = Path + ".tmp";
        await File.WriteAllTextAsync(tempPath, root.ToJsonString(WriteOptions), cancellationToken).ConfigureAwait(false);
        File.Move(tempPath, Path, overwrite: true);
    }
}
