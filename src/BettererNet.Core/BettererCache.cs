using System.Text.Json;
using System.Text.Json.Nodes;

namespace BettererNet;

/// <summary>
/// A machine-local cache of per-test input fingerprints. When a test's fingerprint is unchanged the
/// runner skips it. This is a performance cache, not a baseline — add <c>.betterer.cache</c> to
/// <c>.gitignore</c> rather than committing it.
/// </summary>
public sealed class BettererCache
{
    /// <summary>The conventional cache file name.</summary>
    public const string DefaultFileName = ".betterer.cache";

    private const int CurrentVersion = 1;

    private readonly SortedDictionary<string, string> _fingerprints;

    private BettererCache(string path, SortedDictionary<string, string> fingerprints)
    {
        Path = path;
        _fingerprints = fingerprints;
    }

    public string Path { get; }

    /// <summary>Load the cache, returning an empty instance if it does not yet exist.</summary>
    public static async Task<BettererCache> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var fingerprints = new SortedDictionary<string, string>(StringComparer.Ordinal);

        if (File.Exists(path))
        {
            var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            if (JsonNode.Parse(text) is JsonObject root && root["fingerprints"] is JsonObject stored)
            {
                foreach (var (name, node) in stored)
                {
                    if (node is not null)
                    {
                        fingerprints[name] = node.GetValue<string>();
                    }
                }
            }
        }

        return new BettererCache(path, fingerprints);
    }

    public bool TryGet(string name, out string? fingerprint)
    {
        if (_fingerprints.TryGetValue(name, out var value))
        {
            fingerprint = value;
            return true;
        }

        fingerprint = null;
        return false;
    }

    /// <summary>Store a test's fingerprint. Returns <c>true</c> if it changed.</summary>
    public bool Set(string name, string fingerprint)
    {
        if (_fingerprints.TryGetValue(name, out var existing) && existing == fingerprint)
        {
            return false;
        }

        _fingerprints[name] = fingerprint;
        return true;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var fingerprints = new JsonObject();
        foreach (var (name, fingerprint) in _fingerprints)
        {
            fingerprints[name] = fingerprint;
        }

        var root = new JsonObject { ["version"] = CurrentVersion, ["fingerprints"] = fingerprints };
        var tempPath = Path + ".tmp";
        await File.WriteAllTextAsync(tempPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), cancellationToken).ConfigureAwait(false);
        File.Move(tempPath, Path, overwrite: true);
    }
}
