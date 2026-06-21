using System.Text.Json.Nodes;

namespace BettererNet;

/// <summary>
/// Naive merge of two results files for resolving git conflicts in <c>.betterer.results</c>.
/// Produces the tightest baseline that preserves both sides' improvements: equal values are kept,
/// numbers take the minimum, arrays and objects take their intersection, and a test present on only
/// one side is carried over.
/// </summary>
public static class BettererResultsMerge
{
    /// <summary>Merge the per-test results of two files, returning the merged (canonical) values.</summary>
    public static IReadOnlyDictionary<string, JsonNode> Merge(BettererResultsFile ours, BettererResultsFile theirs)
    {
        var merged = new SortedDictionary<string, JsonNode>(StringComparer.Ordinal);

        foreach (var name in ours.Results.Keys.Union(theirs.Results.Keys, StringComparer.Ordinal))
        {
            var hasOurs = ours.Results.TryGetValue(name, out var a);
            var hasTheirs = theirs.Results.TryGetValue(name, out var b);

            var value = hasOurs && hasTheirs ? MergeValue(a!, b!)
                : hasOurs ? a!.DeepClone()
                : b!.DeepClone();

            if (value is not null)
            {
                merged[name] = value;
            }
        }

        return merged;
    }

    /// <summary>Merge two results files on disk and write the result to <paramref name="outputPath"/>.</summary>
    public static async Task MergeFilesAsync(string oursPath, string theirsPath, string outputPath, CancellationToken cancellationToken = default)
    {
        var ours = await BettererResultsFile.LoadAsync(oursPath, cancellationToken).ConfigureAwait(false);
        var theirs = await BettererResultsFile.LoadAsync(theirsPath, cancellationToken).ConfigureAwait(false);

        var merged = Merge(ours, theirs);
        var output = BettererResultsFile.Create(outputPath);
        foreach (var (name, value) in merged)
        {
            output.Set(name, value);
        }

        await output.SaveAsync(cancellationToken).ConfigureAwait(false);
    }

    private static JsonNode? MergeValue(JsonNode a, JsonNode b)
    {
        if (JsonCanonicalizer.AreEqual(a, b))
        {
            return a.DeepClone();
        }

        if (TryGetInteger(a, out var integerA) && TryGetInteger(b, out var integerB))
        {
            return JsonValue.Create(Math.Min(integerA, integerB));
        }

        if (a is JsonArray arrayA && b is JsonArray arrayB)
        {
            return IntersectArrays(arrayA, arrayB);
        }

        if (a is JsonObject objectA && b is JsonObject objectB)
        {
            return IntersectObjects(objectA, objectB);
        }

        // Incompatible shapes: keep the tighter (fewer items).
        return CountItems(a) <= CountItems(b) ? a.DeepClone() : b.DeepClone();
    }

    private static JsonArray IntersectArrays(JsonArray a, JsonArray b)
    {
        var available = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in b)
        {
            var key = Compact(item);
            available[key] = available.GetValueOrDefault(key) + 1;
        }

        var result = new JsonArray();
        foreach (var item in a)
        {
            var key = Compact(item);
            if (available.TryGetValue(key, out var count) && count > 0)
            {
                available[key] = count - 1;
                result.Add(item?.DeepClone());
            }
        }

        return result;
    }

    private static JsonObject IntersectObjects(JsonObject a, JsonObject b)
    {
        var result = new JsonObject();
        foreach (var (key, value) in a)
        {
            if (b.TryGetPropertyValue(key, out var other) && value is not null && other is not null && MergeValue(value, other) is { } merged)
            {
                result[key] = merged;
            }
        }

        return result;
    }

    private static bool TryGetInteger(JsonNode node, out long value)
    {
        value = 0;
        return node is JsonValue jsonValue && jsonValue.TryGetValue(out value);
    }

    private static int CountItems(JsonNode? node) => node switch
    {
        JsonArray array => array.Count,
        JsonObject obj => obj.Sum(pair => CountItems(pair.Value)),
        null => 0,
        _ => 1,
    };

    private static string Compact(JsonNode? node) => node?.ToJsonString() ?? "null";
}
