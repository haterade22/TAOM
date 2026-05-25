using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TAOM.Features.CrashReport.Collectors;

public static class DllHasher
{
    public static string Sha1OfFile(string? path)
    {
        if (string.IsNullOrEmpty(path)) return "(no-path)";
        try
        {
            if (!File.Exists(path)) return "(missing)";
            using var sha = SHA1.Create();
            using var fs = File.OpenRead(path);
            var bytes = sha.ComputeHash(fs);
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
        catch { return "(hash-failed)"; }
    }
}
