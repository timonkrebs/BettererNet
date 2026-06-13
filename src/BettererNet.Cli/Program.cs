using BettererNet;
using BettererNet.Cli;

var argList = args.ToList();

// `init` scaffolds a config and needs no assembly.
if (argList is ["init", ..])
{
    return Init(Directory.GetCurrentDirectory());
}

// Extract `--config <assembly>` (the compiled config that supplies the tests).
string? configPath = null;
for (var i = 0; i < argList.Count; i++)
{
    if (argList[i] is "--config" or "-c")
    {
        if (i + 1 >= argList.Count)
        {
            Console.Error.WriteLine("Missing value for --config.");
            return 2;
        }

        configPath = argList[i + 1];
        argList.RemoveRange(i, 2);
        break;
    }
}

IEnumerable<IBettererTest> tests = [];
if (configPath is not null)
{
    try
    {
        tests = ConfigLoader.Load(configPath);
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 2;
    }
}

return await BettererCli.RunAsync(argList, tests);

static int Init(string directory)
{
    var path = Path.Combine(directory, "BettererConfig.cs");
    if (File.Exists(path))
    {
        Console.WriteLine($"{path} already exists.");
        return 0;
    }

    File.WriteAllText(path, """
        using System.Collections.Generic;
        using BettererNet;

        // Build this into an assembly, then run: betterernet --config <assembly>.dll ci
        public sealed class BettererConfig : IBettererSuiteProvider
        {
            public IEnumerable<IBettererTest> GetTests()
            {
                yield return BettererRegexTest.Create("NoTodos", "TODO", new[] { "**/*.cs" });
            }
        }
        """);

    Console.WriteLine($"Created {path}.");
    Console.WriteLine("Reference the BettererNet packages, build it, then run: betterernet --config <assembly> ci");
    return 0;
}
