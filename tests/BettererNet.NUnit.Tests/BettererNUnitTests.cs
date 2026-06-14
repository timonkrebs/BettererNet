using System.Text.Json.Nodes;
using BettererNet;
using NUnit.Framework;

namespace BettererNet.NUnitTests;

public class BettererNUnitTests
{
    private string _dir = null!;

    private string ResultsPath => Path.Combine(_dir, BettererResultsFile.DefaultFileName);

    [SetUp]
    public void SetUp() => _dir = Directory.CreateTempSubdirectory("betterernet").FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_dir, recursive: true);

    private async Task Seed(string name, long value)
    {
        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        file.Set(name, JsonValue.Create(value));
        await file.SaveAsync();
    }

    [Test]
    public async Task Regression_ThrowsNUnitAssertion()
    {
        await Seed("count", 2);

        Assert.ThrowsAsync<AssertionException>(() =>
            new Betterer("count", ResultsPath).AssertAsync(BettererCountTest.Create("count", () => 5)));
    }

    [Test]
    public async Task Improvement_PassesAndRatchets()
    {
        await Seed("count", 5);

        await new Betterer("count", ResultsPath).AssertAsync(BettererCountTest.Create("count", () => 3));

        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        Assert.That(file.Results["count"].GetValue<long>(), Is.EqualTo(3));
    }
}
