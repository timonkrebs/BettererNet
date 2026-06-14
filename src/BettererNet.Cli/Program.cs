using BettererNet;
using BettererNet.Cli;

var argList = args.ToList();

// `init` and `merge` need no config; let BettererCli handle them.
if (argList is ["init", ..] or ["merge", ..])
{
    return await BettererCli.RunAsync(argList, []);
}

// Resolve the config: an explicit `--config`, otherwise an auto-detected `betterer.json`.
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

configPath ??= new[] { "betterer.json", "betterer.jsonc" }.FirstOrDefault(File.Exists);

IEnumerable<IBettererTest> tests = [];
string? declaredResults = null;
if (configPath is not null)
{
    try
    {
        if (BettererConfigFile.IsConfigFile(configPath))
        {
            (tests, declaredResults) = BettererConfigFile.Load(configPath);
        }
        else
        {
            tests = ConfigLoader.Load(configPath);
        }
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 2;
    }
}

// A results path declared in the config is a default; an explicit --results on the CLI wins.
if (declaredResults is not null && !argList.Any(arg => arg is "--results" or "-r"))
{
    argList.Add("--results");
    argList.Add(declaredResults);
}

return await BettererCli.RunAsync(argList, tests);
