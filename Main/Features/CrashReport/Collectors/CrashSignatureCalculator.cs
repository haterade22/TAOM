using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using TAOM.Features.CrashReport.Domain;

namespace TAOM.Features.CrashReport.Collectors;

// Deterministic ID for a crash: SHA1 of (ExceptionType, throwingMethodFullName,
// top-5 frame method names). Lets us dedup crashes across reports — players
// hitting the same bug all produce the same signature even with different
// random campaign state.
public static class CrashSignatureCalculator
{
    public const int FrameDepth = 5;

    public static string Compute(string exceptionType, string originatingPatchTarget, IReadOnlyList<StackFrameSnapshot> stack)
    {
        var sb = new StringBuilder();
        sb.Append(exceptionType).Append('|').Append(originatingPatchTarget).Append('|');
        int taken = 0;
        foreach (var f in stack)
        {
            sb.Append(f.MethodFullName ?? "?").Append(';');
            if (++taken >= FrameDepth) break;
        }
        using var sha = SHA1.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        // 8-char prefix is enough for bundle filename; full hash kept for log.
        var full = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) full.Append(b.ToString("x2"));
        return full.ToString();
    }

    public static string Short(string fullSignature) => fullSignature.Length >= 8 ? fullSignature.Substring(0, 8) : fullSignature;
}
