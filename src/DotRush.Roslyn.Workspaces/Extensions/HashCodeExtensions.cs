using System.Security.Cryptography;

namespace DotRush.Roslyn.Workspaces.Extensions;

public static class HashCodeExtensions {
#pragma warning disable CA5350 // SHA1 is not used for security purposes
    public static string GetHashForFile(string filePath) {
        using var stream = File.OpenRead(filePath);

        var sha = SHA1.Create();
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

#pragma warning restore CA5350
}