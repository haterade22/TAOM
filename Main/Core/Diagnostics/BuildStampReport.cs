using System;
using System.Collections.Generic;
using System.Diagnostics;
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
/// Directory.Build.props stamps <c>InformationalVersion</c> as <c>build.yyyyMMdd-HHmmssZ</c>; the
/// .NET SDK then appends <c>+{commit-sha}</c>, marked <c>.dirty</c> or <c>.nogit</c> by
/// Directory.Build.props when it must, so the stamp is NOT at the end of the string. Both modules
/// are produced by the same build, so their stamps should agree to within seconds; a gap of hours
/// means a hand-copied module. Equal clean commits are a pair whatever the time between the builds;
/// the time tiers remain the fallback.
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
    public static string DescribeVerdict(BuildPairVerdict verdict, DateTime a, DateTime b,
        string? mainCommit = null, string? depsCommit = null)
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
                string commits = mainCommit != null && depsCommit != null
                    ? $" from different commits ({mainCommit}, {depsCommit})" : "";
                return $" MISMATCH — built {gap} apart{commits}. These modules were not built together: install " +
                       "BOTH from one release. If a preview patch below did not apply, this pairing is the likely " +
                       "cause (issue #371).";

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

        // Fixed-width slice, NOT a trim. The .NET SDK appends the commit SHA to InformationalVersion
        // ("+{sha}", or ".{sha}" when the string already holds a '+', as the 2026-08-02 form did),
        // so the real string is "build.20260802-013132Z.46ce6436…", now "build.…Z+{sha}[.dirty]".
        // An earlier version did Substring(marker).TrimEnd('Z'), which left the whole SHA attached
        // and failed to parse every real assembly — while the unit tests passed, because they
        // asserted against the format this code ASSUMED rather than the one the build emits.
        string tail = informationalVersion.Substring(i + StampMarker.Length);
        if (tail.Length < StampLength) return false;

        return DateTime.TryParseExact(tail.Substring(0, StampLength), StampFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out stamp);
    }

    private static readonly System.Text.RegularExpressions.Regex CommitRx = new(
        @"build\.\d{8}-\d{6}Z[+.](?<sha>[0-9a-fA-F]{40})(?<flag>\.(?:dirty|nogit))?",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>
    /// Extracts the commit from an <c>InformationalVersion</c>. <paramref name="flagged"/> is true
    /// when the build marked its tree <c>.dirty</c> or <c>.nogit</c>, so the SHA proves nothing.
    /// </summary>
    public static bool TryParseCommit(string? informationalVersion, out string commit, out bool flagged)
    {
        commit = string.Empty;
        flagged = false;
        if (string.IsNullOrEmpty(informationalVersion)) return false;

        var m = CommitRx.Match(informationalVersion);
        if (!m.Success) return false;

        commit = m.Groups["sha"].Value.ToLowerInvariant();
        flagged = m.Groups["flag"].Success;
        return true;
    }

    /// <summary>
    /// Reads both assemblies' informational versions and reports. Returns the message so callers can
    /// route it to whatever logger they have; never throws.
    /// </summary>
    public static string BuildReport(Assembly? main, Assembly? dependencies)
    {
        return BuildReportFromParts(
            ReadInformationalVersion(main), ReadBuildConfiguration(main),
            ReadInformationalVersion(dependencies), ReadBuildConfiguration(dependencies));
    }

    /// <summary>
    /// The pure composition of the startup line. The configuration tokens sit after each stamp, so
    /// <c>TAOM=\S+ build\.</c> and the <c> TAOM.Dependencies=</c> boundary still parse.
    /// </summary>
    internal static string BuildReportFromParts(string mainVer, string mainConfig, string depsVer, string depsConfig)
    {
        string verdict;
        if (TryParseStamp(mainVer, out var mainStamp) && TryParseStamp(depsVer, out var depsStamp))
        {
            bool mainHasCommit = TryParseCommit(mainVer, out var mainCommit, out var mainFlagged);
            bool depsHasCommit = TryParseCommit(depsVer, out var depsCommit, out var depsFlagged);
            bool bothHaveCommits = mainHasCommit && depsHasCommit;
            bool sameCommit = bothHaveCommits && mainCommit == depsCommit;

            if (sameCommit && !mainFlagged && !depsFlagged)
            {
                verdict = $" (pair OK: both built from {mainCommit.Substring(0, 8)})";
            }
            else if ((mainFlagged || depsFlagged) && Classify(mainStamp, depsStamp) == BuildPairVerdict.Paired)
            {
                // A flagged stamp cannot prove a pair, so close-in-time builds never read as "pair OK".
                verdict = " (pair close in time, stamp flagged)";
            }
            else
            {
                bool namesCommits = bothHaveCommits && !sameCommit;
                verdict = DescribeVerdict(Classify(mainStamp, depsStamp), mainStamp, depsStamp,
                    namesCommits ? mainCommit.Substring(0, 8) : null,
                    namesCommits ? depsCommit.Substring(0, 8) : null);
            }
        }
        else
        {
            verdict = " (no build stamp on one or both — pre-2026-08-01 build, cannot verify pairing)";
        }

        return $"[BuildStamp] TAOM={mainVer} {mainConfig} TAOM.Dependencies={depsVer} {depsConfig}{verdict}";
    }

    /// <summary>
    /// Renders a DLL's build configuration and JIT state as two tokens. Public builds ship Debug on
    /// purpose, and every frame time from one measures unoptimized code, so a log has to say so.
    /// </summary>
    public static string DescribeBuildConfiguration(string? configuration, bool? jitOptimizerDisabled)
    {
        string config = string.IsNullOrWhiteSpace(configuration)
            ? "unknown"
            : string.Join("_", configuration!.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        string jit = jitOptimizerDisabled is null ? "unknown" : (jitOptimizerDisabled.Value ? "OFF" : "ON");
        return $"config={config} jitOpt={jit}";
    }

    internal static string ReadBuildConfiguration(Assembly? asm)
    {
        if (asm == null) return DescribeBuildConfiguration(null, null);
        try
        {
            string? config = asm.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration;
            // No DebuggableAttribute: the JIT optimizes. Same rule as the 1.5.x PerfContextReader.
            bool jitOff = asm.GetCustomAttribute<DebuggableAttribute>()?.IsJITOptimizerDisabled ?? false;
            return DescribeBuildConfiguration(config, jitOff);
        }
        catch { return DescribeBuildConfiguration(null, null); }
    }

    /// <summary>
    /// Renders the engine identity line: which Bannerlord build this process is running, and which
    /// modules are loaded alongside TAOM.
    ///
    /// This exists because of a 2026-08-19 player report. The player sent a debug log, but the
    /// session never started a game, so <c>MissionDiagnosticService.LogSessionSnapshot</c> (the only
    /// place the engine version was recorded) never ran. The log named the TAOM build precisely and
    /// the Bannerlord build not at all, which is exactly backwards: TAOM's own version is knowable
    /// from the stamp in the filename or the module folder, while the engine version is the fact
    /// that decides whether a report is an engine-drift case or a real bug.
    ///
    /// Deliberately pure and string-typed. The caller does the <c>ModuleHelper</c> lookups, so
    /// nothing here needs the engine to be loaded and all of it is unit-testable.
    /// </summary>
    /// <param name="nativeVersion">
    /// The Native module's version, typically <c>ModuleHelper.GetModuleInfo("Native")?.Version</c>.
    /// Null-propagating at the call site, so null and blank are expected inputs, not defects.
    /// </param>
    /// <param name="activeModules">Pre-formatted "Id vVersion" descriptors, in load order.</param>
    public static string EngineReport(string? nativeVersion, IEnumerable<string>? activeModules)
    {
        string engine = string.IsNullOrWhiteSpace(nativeVersion) ? "unknown" : nativeVersion!.Trim();

        // Materialise defensively: the caller hands us whatever the engine's enumerator yields this
        // early in startup, and we need a stable count and a stable join over the same sequence.
        var modules = new List<string>();
        if (activeModules != null)
        {
            foreach (var descriptor in activeModules)
            {
                if (!string.IsNullOrWhiteSpace(descriptor)) modules.Add(descriptor!.Trim());
            }
        }

        // No cap on the module list. A 50-mod load order is normal for this game, and the whole
        // point of the line is answering "what else was loaded", which a truncation would defeat.
        return $"[Engine] Bannerlord={engine} modules({modules.Count})=[{string.Join(", ", modules.ToArray())}]";
    }

    internal static string ReadInformationalVersion(Assembly? asm)
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
