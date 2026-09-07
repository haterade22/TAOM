using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using TAOM.Features.CrashReport.Domain;

namespace TAOM.Features.CrashReport.Collectors;

// Deterministic ID for a crash: SHA1 of (exception identity, throwingMethodFullName,
// top-5 frame method names). Lets us dedup crashes across reports — players
// hitting the same bug all produce the same signature even with different
// random campaign state.
public static class CrashSignatureCalculator
{
    public const int FrameDepth = 5;

    /// <summary>
    /// How far down the inner-exception chain the identity reaches. Bounded because this runs inside
    /// the crash handler, where an unbounded walk is the worst thing to get wrong. Three is enough
    /// for the shapes that actually occur: a Harmony or reflection wrapper, the real fault, and one
    /// more level of wrapping under that.
    /// </summary>
    public const int InnerChainDepth = 3;

    /// <summary>
    /// The identity a crash gets deduplicated by, derived from the exception itself so the inner
    /// chain is part of it.
    ///
    /// WHY THE INNER CHAIN IS LOAD-BEARING (issue #552, from crash bundle 31942985): every crash
    /// dispatched through the Gauntlet UI arrives as a <c>TargetInvocationException</c> whose stack
    /// is the same eight frames from <c>ViewModel.ExecuteCommand</c> down to
    /// <c>ScreenManager.Update</c>. Two crashes there differ only in what the invoked method threw.
    /// Hashing the outer type alone made them one signature, and <c>CrashBundleThrottle</c>
    /// suppressed the second — which in that bundle was the <c>IndexOutOfRangeException</c> that
    /// named the state corruption behind the fatal CTD three seconds later. The chain was only
    /// reconstructable because the raw engine log happened to ride along in the surviving bundle.
    ///
    /// Deduplication itself stays intact: identical crashes still collapse to one bundle, which is
    /// what stops a per-frame crash re-zipping an ever-growing debug log.
    /// </summary>
    public static string Compute(Exception exception, string originatingPatchTarget, IReadOnlyList<StackFrameSnapshot> stack)
        => Compute(DescribeIdentity(exception), originatingPatchTarget, stack);

    /// <summary>
    /// Outer type, then each inner type and its target site, to <see cref="InnerChainDepth"/>.
    /// An exception with no inner chain yields exactly its type name, so signatures already in the
    /// wild keep pointing at the same crash.
    ///
    /// Every read is defensive: a custom exception type can throw inside its own getters, and the
    /// one place that must never throw is the handler reporting someone else's crash.
    /// </summary>
    public static string DescribeIdentity(Exception exception)
    {
        if (exception == null)
            return "(unknown)";

        var sb = new StringBuilder();
        var current = exception;
        for (var depth = 0; current != null && depth <= InnerChainDepth; depth++)
        {
            if (depth > 0)
                sb.Append("<-");

            sb.Append(SafeRead(() => current.GetType().FullName) ?? "(unknown type)");

            if (depth > 0)
                sb.Append('@').Append(SafeRead(() => TargetSiteOf(current)) ?? "?");

            Exception inner;
            try { inner = current.InnerException; } catch { break; }

            // A chain that points back at something already seen would otherwise loop forever.
            if (ReferenceEquals(inner, current))
                break;

            current = inner;
        }

        return sb.ToString();
    }

    private static string TargetSiteOf(Exception ex)
    {
        var site = ex.TargetSite;
        if (site == null)
            return null;
        return (site.DeclaringType?.FullName ?? "?") + "." + site.Name;
    }

    private static string SafeRead(Func<string> read)
    {
        try { return read(); } catch { return null; }
    }

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
