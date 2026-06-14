using System.Text.Json.Nodes;
using BettererNet;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace BettererNet.TUnitTests;

public sealed class BettererTUnitTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("betterernet").FullName;

    private string ResultsPath => Path.Combine(_dir, BettererResultsFile.DefaultFileName);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private async Task Seed(string name, long value)
    {
        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        file.Set(name, JsonValue.Create(value));
        await file.SaveAsync();
    }

    [Test]
    public async Task Regression_ThrowsTUnitAssertion()
    {
        await Seed("count", 2);

        Exception? caught = null;
        try
        {
            await new Betterer("count", ResultsPath).AssertAsync(BettererCountTest.Create("count", () => 5));
        }
        catch (Exception error)
        {
            caught = error;
        }

        await Assert.That(caught).IsNotNull();
    }

    [Test]
    public async Task Improvement_PassesAndRatchets()
    {
        await Seed("count", 5);

        await new Betterer("count", ResultsPath).AssertAsync(BettererCountTest.Create("count", () => 3));

        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        await Assert.That(file.Results["count"].GetValue<long>()).IsEqualTo(3L);
    }
}
