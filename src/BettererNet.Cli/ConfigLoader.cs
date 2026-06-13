using System.Reflection;
using System.Runtime.Loader;

namespace BettererNet.Cli;

/// <summary>Loads the tests from a compiled config assembly (the .NET analog of betterer's `.betterer.ts`).</summary>
public static class ConfigLoader
{
    public static IEnumerable<IBettererTest> Load(string configAssemblyPath)
    {
        var fullPath = Path.GetFullPath(configAssemblyPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Config assembly not found: {fullPath}");
        }

        var context = new ConfigLoadContext(fullPath);
        var assembly = context.LoadFromAssemblyPath(fullPath);

        var providerType = assembly.GetTypes().FirstOrDefault(type =>
            typeof(IBettererSuiteProvider).IsAssignableFrom(type) && type is { IsAbstract: false, IsInterface: false });

        if (providerType is null)
        {
            throw new InvalidOperationException(
                $"No public IBettererSuiteProvider implementation found in '{configAssemblyPath}'.");
        }

        var provider = (IBettererSuiteProvider)Activator.CreateInstance(providerType)!;
        return provider.GetTests().ToList();
    }

    // Resolves the config's own dependencies from its directory, while sharing the BettererNet
    // contract assemblies with the host so IBettererTest unifies across the two load contexts.
    private sealed class ConfigLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public ConfigLoadContext(string configPath)
            : base(isCollectible: false) => _resolver = new AssemblyDependencyResolver(configPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name?.StartsWith("BettererNet", StringComparison.Ordinal) == true)
            {
                return null; // defer to the host's already-loaded contract assemblies
            }

            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }
    }
}
