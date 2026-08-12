using System;
using System.Globalization;
using System.Reflection;

namespace TAOM.Core.Diagnostics;

/// <summary>
/// Reports the build stamp of TAOM and TAOM.Dependencies at startup, and flags a mismatched pair.
///
/// Issue #371: TAOM resolves HarmonyLib and Bannerlord.UIExtenderEx THROUGH TAOM.Dependencies
/// (see the ProjectReference in Main/TAOM.csproj). Pair a current TAOM.dll with a stale
/// TAOM.Dependencies.dll and type/member resolution fails during patch application — the preview
/// patches never attach and every character renders in bind pose. Both assemblies carried frozen
/// versions (2.0.0.0 / 0.1.0.0) on every build ever produced, so .NET bound any pair without
/// complaint and nothing on disk could tell a current DLL from a two-week-old one.
///
/// Directory.Build.props stamps <c>InformationalVersion</c> as <c>build.yyyyMMdd-HHmmssZ</c>;
/// Bannerlord.BuildResources then appends <c>.{commit-sha}</c>, so the stamp is NOT at the end of
/// the string. Both modules are produced by the same build, so their stamps should agree to within
/// seconds; a gap of hours means a hand-copied module.
/// </summary>
/// <summary>How a pair of build stamps relates. Three tiers, because two of them are benign.</summary>
public enum BuildPairVerdict
{
    /// <summary>Stamped within minutes: one build.ps1 run produced both.</summary>
    Paired = 0,

    /// <summary>
    /// Hours apart on one working day. A developer rebuilt one module after an edit that may not
    /// have touched code at all. Worth stating, not worth shouting about.
    /// </summary>
    IncrementalRebuild = 1,

    /// <summary>Days apart: the real #371 shape, a hand-copied module from an older release.</summary>
    Mismatched = 2,
}

public static class BuildStampReport
{
    /// <summary>Stamps further apart than this almost certainly come from different builds.</summary>
    public static readonly TimeSpan MismatchTolerance = TimeSpan.FromHours(1);

    /// <summary>
    /// Upper bound on a same-day rebuild. Past this the pair is treated as genuinely stale.
    ///
    /// The runtime cannot answer the question that actually matters — "did any CODE change between
    /// these two commits?" — because that needs git, and the game has no repo. Elapsed time is the
    /// only proxy available, so the tiers split on the shape each failure actually has: a developer
    /// rebuilding one module mid-session is hours apart, while the stale module #371 was opened for
    /// was two weeks apart. Reporting a docs-only rebuild in the same words as a two-week-old DLL
    /// is what trains a reader to skip the line, and a warning nobody reads protects nothing.
    /// </summary>
    public static readonly TimeSpan RebuildTolerance = TimeSpan.FromHours(12);

    /// <summary>Which tier a pair of stamps falls into. Pure and symmetric in its arguments.</summary>
    public static BuildPairVerdict Classify(DateTime a, DateTime b)
    {
        var gap = a > b ? a - b : b - a;
        if (gap <= MismatchTolerance) return BuildPairVerdict.Paired;
        return gap <= RebuildTolerance ? BuildPairVerdict.IncrementalRebuild : BuildPairVerdict.Mismatched;
    }

    /// <summary>
    /// Renders a verdict as the tail of the startup line. Only <see cref="BuildPairVerdict.Mismatched"/>
    /// carries the word MISMATCH, which is what makes that word mean something when it appears.
    /// </summary>
    public static string DescribeVerdict(BuildPairVerdict verdict, DateTime a, DateTime b)
    {
        string gap = (a > b ? a - b : b - a).ToString(@"d\d\ hh\h\ mm\m", CultureInfo.InvariantCulture);

        switch (verdict)
        {
            case BuildPairVerdict.Paired:
                return " (pair OK)";

            case BuildPairVerdict.IncrementalRebuild:
                return $" (pair rebuilt {gap} apart — one module was rebuilt after the other, " +
                       "which is normal mid-session and is usually a docs or config commit. " +
                       "Rebuild both if the preview patches misbehave. Issue #371.)";

            case BuildPairVerdict.Mismatched:
                return $" MISMATCH — built {gap} apart. These modules were not built together; " +
                       "update BOTH from the same release or expect the preview patches to fail " +
                       "(issue #371).";

            // Explicit, so a future fourth tier cannot inherit the loudest wording by accident.
            // MISMATCH is an escalation signal; letting an unhandled value render as one is how a
            // warning stops meaning anything.
            default:
                return $" (unrecognised build-pair verdict '{verdict}', {gap} apart — pairing not " +
                       "assessed; teach DescribeVerdict about this tier. Issue #371.)";
        }
    }

    // "build." not "+build.": the marker must match whether or not a version prefix precedes it,
    // and the suffix separator the build tooling appends varies ('.' or '+') depending on whether
    // the string already contains a '+'. Matching the bare token is stable across both.
    private const string StampMarker = "build.";
    private const string StampFormat = "yyyyMMdd-HHmmss";
    private const int StampLength = 15;   // yyyyMMdd-HHmmss

    /// <summary>
    /// Extracts the build timestamp from an <c>InformationalVersion</c>. Pure, so the parsing and
    /// the comparison policy are testable without a build.
    /// </summary>
    public static bool TryParseStamp(string? informationalVersion, out DateTime stamp)
    {
        stamp = default;
        if (string.IsNullOrEmpty(informationalVersion)) return false;

        int i = informationalVersion!.IndexOf(StampMarker, StringComparison.Ordinal);
        if (i < 0) return false;

        // Fixed-width slice, NOT a trim. Bannerlord.BuildResources appends its own ".{commit-sha}"
        // suffix to InformationalVersion, so the real string is "build.20260802-013132Z.46ce6436…".
        // An earlier version did Substring(marker).TrimEnd('Z'), which left the whole SHA attached
        // and failed to parse every real assembly — while the unit tests passed, because they
        // asserted against the format this code ASSUMED rather than the one the build emits.
        string tail = informationalVersion.Substring(i + StampMarker.Length);
        if (tail.Length < StampLength) return false;

        return DateTime.TryParseExact(tail.Substring(0, StampLength), StampFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out stamp);
    }

    /// <summary>
    /// Reads both assemblies' informational versions and reports. Returns the message so callers can
    /// route it to whatever logger they have; never throws.
    /// </summary>
    public static string BuildReport(Assembly? main, Assembly? dependencies)
    {
        string mainVer = ReadInformationalVersion(main);
        string depsVer = ReadInformationalVersion(dependencies);

        string verdict;
        if (TryParseStamp(mainVer, out var mainStamp) && TryParseStamp(depsVer, out var depsStamp))
        {
            verdict = DescribeVerdict(Classify(mainStamp, depsStamp), mainStamp, depsStamp);
        }
        else
        {
            verdict = " (no build stamp on one or both — pre-2026-08-01 build, cannot verify pairing)";
        }

        return $"[BuildStamp] TAOM={mainVer} TAOM.Dependencies={depsVer}{verdict}";
    }

    private static string ReadInformationalVersion(Assembly? asm)
    {
        if (asm == null) return "<not loaded>";
        try
        {
            // Report BOTH: Directory.Build.props is imported before the csproj's own
            // PropertyGroup, so $(Version) is not yet defined there and cannot be folded into the
            // stamp. The assembly version carries the product version; the informational version
            // carries the per-build identity.
            string asmVer = asm.GetName().Version?.ToString() ?? "<unknown>";
            var attr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return string.IsNullOrEmpty(attr?.InformationalVersion)
                ? asmVer
                : $"v{asmVer} {attr!.InformationalVersion}";
        }
        catch { return "<unreadable>"; }
    }
}
