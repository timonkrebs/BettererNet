using System.Security.Cryptography;
using System.Text;

namespace BettererNet;

/// <summary>Stable, short content hashes for issue tracking (not <c>GetHashCode</c>, which is randomized).</summary>
internal static class BettererHash
{
    /// <summary>A stable 16-character hex hash of <paramref name="input"/>.</summary>
    public static string Compute(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes.AsSpan(0, 8)).ToLowerInvariant();
    }
}
