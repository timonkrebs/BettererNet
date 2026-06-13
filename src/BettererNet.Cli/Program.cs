using BettererNet;
using BettererNet.Cli;

var argList = args.ToList();

// `init` and `merge` need no config assembly; let BettererCli handle them.
if (argList is ["init", ..] or ["merge", ..])
{
    return await BettererCli.RunAsync(argList, []);
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
