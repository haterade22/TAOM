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
/// Directory.Build.props now stamps <c>InformationalVersion</c> as
/// <c>&lt;version&gt;+build.yyyyMMdd-HHmmssZ</c>. Both modules are produced by the same build, so
/// their stamps should agree to within seconds; a gap of hours means a hand-copied module.
/// </summary>
public static class BuildStampReport
{
    /// <summary>Stamps further apart than this almost certainly come from different builds.</summary>
    public static readonly TimeSpan MismatchTolerance = TimeSpan.FromHours(1);

    private const string StampMarker = "+build.";
    private const string StampFormat = "yyyyMMdd-HHmmss";

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

        string raw = informationalVersion.Substring(i + StampMarker.Length).TrimEnd('Z');
        return DateTime.TryParseExact(raw, StampFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out stamp);
    }

    /// <summary>
    /// True when two build stamps are far enough apart to indicate the modules were not built
    /// together. Pure.
    /// </summary>
    public static bool IsMismatched(DateTime a, DateTime b, TimeSpan tolerance)
        => (a > b ? a - b : b - a) > tolerance;

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
            verdict = IsMismatched(mainStamp, depsStamp, MismatchTolerance)
                ? $" MISMATCH — built {(mainStamp > depsStamp ? mainStamp - depsStamp : depsStamp - mainStamp):d\\d\\ hh\\h\\ mm\\m} apart. " +
                  "These modules were not built together; update BOTH from the same release or expect " +
                  "the preview patches to fail (issue #371)."
                : " (pair OK)";
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
            var attr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return string.IsNullOrEmpty(attr?.InformationalVersion)
                ? asm.GetName().Version?.ToString() ?? "<unknown>"
                : attr!.InformationalVersion;
        }
        catch { return "<unreadable>"; }
    }
}
