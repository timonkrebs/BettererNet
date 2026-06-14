using System.Text.Json.Nodes;
using BettererNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BettererNet.MSTestTests;

[TestClass]
public class BettererMSTestTests
{
    private string _dir = null!;

    private string ResultsPath => Path.Combine(_dir, BettererResultsFile.DefaultFileName);

    [TestInitialize]
    public void SetUp() => _dir = Directory.CreateTempSubdirectory("betterernet").FullName;

    [TestCleanup]
    public void TearDown() => Directory.Delete(_dir, recursive: true);

    private async Task Seed(string name, long value)
    {
        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        file.Set(name, JsonValue.Create(value));
        await file.SaveAsync();
    }

    [TestMethod]
    public async Task Regression_ThrowsMSTestAssertion()
    {
        await Seed("count", 2);

        var threw = false;
        try
        {
            await new Betterer("count", ResultsPath).AssertAsync(BettererCountTest.Create("count", () => 5));
        }
        catch (AssertFailedException)
        {
            threw = true;
        }

        Assert.IsTrue(threw);
    }

    [TestMethod]
    public async Task Improvement_PassesAndRatchets()
    {
        await Seed("count", 5);

        await new Betterer("count", ResultsPath).AssertAsync(BettererCountTest.Create("count", () => 3));

        var file = await BettererResultsFile.LoadAsync(ResultsPath);
        Assert.AreEqual(3L, file.Results["count"].GetValue<long>());
    }
}
