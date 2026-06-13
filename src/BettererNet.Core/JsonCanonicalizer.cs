using System.Text.Json.Nodes;

namespace BettererNet;

/// <summary>
/// Produces a canonical (order-independent) form of a JSON value: object keys are sorted and
/// array elements are sorted by their serialized form. This keeps the results file diff-stable
/// and merge-friendly regardless of the order a test reports its data in.
/// </summary>
public static class JsonCanonicalizer
{
    /// <summary>Return a detached, canonicalized copy of <paramref name="node"/>.</summary>
    public static JsonNode? Canonicalize(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return null;

            case JsonObject obj:
                var sortedObject = new JsonObject();
                foreach (var key in obj.Select(pair => pair.Key).OrderBy(key => key, StringComparer.Ordinal))
                {
                    sortedObject[key] = Canonicalize(obj[key]);
                }

                return sortedObject;

            case JsonArray array:
                var items = new List<JsonNode?>(array.Count);
                foreach (var item in array)
                {
                    items.Add(Canonicalize(item));
                }

                items.Sort(static (left, right) => string.CompareOrdinal(Compact(left), Compact(right)));

                var sortedArray = new JsonArray();
                foreach (var item in items)
                {
                    sortedArray.Add(item);
                }

                return sortedArray;

            default:
                // JsonValue: parse a fresh, detached copy.
                return JsonNode.Parse(node.ToJsonString());
        }
    }

    /// <summary>Whether two JSON values are equal after canonicalization (order-independent).</summary>
    public static bool AreEqual(JsonNode? left, JsonNode? right) =>
        string.Equals(Compact(Canonicalize(left)), Compact(Canonicalize(right)), StringComparison.Ordinal);

    private static string Compact(JsonNode? node) => node?.ToJsonString() ?? "null";
}
