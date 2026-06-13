using System.Text.Json.Nodes;
using Xunit;

namespace BettererNet.Tests;

public sealed class JsonCanonicalizerTests
{
    [Fact]
    public void SortsObjectKeys()
    {
        var node = JsonNode.Parse("""{"b":1,"a":2}""");

        Assert.Equal("""{"a":2,"b":1}""", JsonCanonicalizer.Canonicalize(node)!.ToJsonString());
    }

    [Fact]
    public void SortsArrayElements()
    {
        var node = JsonNode.Parse("""["c","a","b"]""");

        Assert.Equal("""["a","b","c"]""", JsonCanonicalizer.Canonicalize(node)!.ToJsonString());
    }

    [Fact]
    public void SortsNestedStructures()
    {
        var node = JsonNode.Parse("""{"z":[3,1,2],"a":{"y":1,"x":2}}""");

        Assert.Equal("""{"a":{"x":2,"y":1},"z":[1,2,3]}""", JsonCanonicalizer.Canonicalize(node)!.ToJsonString());
    }

    [Fact]
    public void AreEqual_IsOrderIndependent()
    {
        Assert.True(JsonCanonicalizer.AreEqual(JsonNode.Parse("""["a","b"]"""), JsonNode.Parse("""["b","a"]""")));
        Assert.False(JsonCanonicalizer.AreEqual(JsonNode.Parse("""["a"]"""), JsonNode.Parse("""["a","b"]""")));
    }
}
