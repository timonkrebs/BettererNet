using System.Security.Cryptography;
using System.Text;

namespace BettererNet;

/// <summary>
/// Computes a content-based fingerprint of a set of input files for the run cache. The fingerprint
/// changes whenever any input file's content changes (or a file is added/removed), and is
/// order-independent.
/// </summary>
public static class BettererFileFingerprint
{
    public static string Compute(IEnumerable<string> files)
    {
        var builder = new StringBuilder();
        foreach (var file in files.Select(Path.GetFullPath).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal))
        {
            builder.Append(file).Append(' ').Append(File.Exists(file) ? ContentHash(file) : "missing").Append('\n');
        }

        return BettererHash.Compute(builder.ToString());
    }

    private static string ContentHash(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
