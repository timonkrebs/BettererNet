using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BettererNet;

/// <summary>One recorded point in a test suite's history: per-test issue/value counts at an instant.</summary>
public sealed record BettererHistorySnapshot
{
    public required DateTimeOffset Timestamp { get; init; }

    public required IReadOnlyDictionary<string, long> Counts { get; init; }
}

/// <summary>
/// An append-only history of suite snapshots, used to show the trend (debt burning down) over time
/// and render a markdown report.
/// </summary>
public sealed class BettererHistory
{
    public const string DefaultFileName = ".betterer.history.json";

    private const int CurrentVersion = 1;

    private readonly List<BettererHistorySnapshot> _snapshots;

    private BettererHistory(string path, List<BettererHistorySnapshot> snapshots)
    {
        Path = path;
        _snapshots = snapshots;
    }

    public string Path { get; }

    public IReadOnlyList<BettererHistorySnapshot> Snapshots => _snapshots;

    public static async Task<BettererHistory> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var snapshots = new List<BettererHistorySnapshot>();

        if (File.Exists(path)
            && JsonNode.Parse(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)) is JsonObject root
            && root["snapshots"] is JsonArray array)
        {
            foreach (var node in array.OfType<JsonObject>())
            {
                var timestamp = node["timestamp"] is JsonValue timeValue && timeValue.TryGetValue<DateTimeOffset>(out var parsed)
                    ? parsed
                    : default;

                var counts = new Dictionary<string, long>(StringComparer.Ordinal);
                if (node["counts"] is JsonObject countsObject)
                {
                    foreach (var (name, value) in countsObject)
                    {
                        if (value is JsonValue countValue && countValue.TryGetValue<long>(out var count))
                        {
                            counts[name] = count;
                        }
                    }
                }

                snapshots.Add(new BettererHistorySnapshot { Timestamp = timestamp, Counts = counts });
            }
        }

        return new BettererHistory(path, snapshots);
    }

    public void Append(BettererHistorySnapshot snapshot) => _snapshots.Add(snapshot);

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var array = new JsonArray();
        foreach (var snapshot in _snapshots)
        {
            var counts = new JsonObject();
            foreach (var (name, value) in snapshot.Counts.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                counts[name] = value;
            }

            array.Add(new JsonObject { ["timestamp"] = snapshot.Timestamp, ["counts"] = counts });
        }

        var root = new JsonObject { ["version"] = CurrentVersion, ["snapshots"] = array };
        var tempPath = Path + ".tmp";
        await File.WriteAllTextAsync(tempPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), cancellationToken).ConfigureAwait(false);
        File.Move(tempPath, Path, overwrite: true);
    }

    /// <summary>Render the most recent snapshots as a markdown trend table (one row per run).</summary>
    public string RenderMarkdown(int maxRows = 20)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Betterer trend");
        builder.AppendLine();

        var tests = _snapshots.SelectMany(snapshot => snapshot.Counts.Keys).Distinct().OrderBy(name => name, StringComparer.Ordinal).ToList();
        var rows = _snapshots.TakeLast(maxRows).ToList();
        if (tests.Count == 0 || rows.Count == 0)
        {
            builder.AppendLine("_No history yet._");
            return builder.ToString();
        }

        builder.Append("| When |");
        foreach (var test in tests)
        {
            builder.Append(' ').Append(test).Append(" |");
        }

        builder.AppendLine();
        builder.Append("| --- |");
        foreach (var _ in tests)
        {
            builder.Append(" --- |");
        }

        builder.AppendLine();
        foreach (var snapshot in rows)
        {
            builder.Append("| ").Append(snapshot.Timestamp.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).Append(" |");
            foreach (var test in tests)
            {
                builder.Append(' ').Append(snapshot.Counts.TryGetValue(test, out var value) ? value.ToString(CultureInfo.InvariantCulture) : "-").Append(" |");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }
}
