using System;
using System.Linq;
using TAOM.Core.Logging;

namespace TAOM.Features.CoopInterop;

/// <summary>
/// Writes the settings fingerprint to the log when a co-op module is active.
/// </summary>
/// <remarks>
/// <para>This is the whole feature today, and deliberately so. The documented workaround for
/// settings divergence is "compare settings manually before playing", which means two people
/// reading 106 values off two screens — nobody does it. One line per group in each peer's log
/// turns that into comparing a handful of short codes, and it needs no save-format change, no
/// handshake and no UI.</para>
///
/// <para>What it does NOT do: read the other peer's fingerprint. Storing it in save metadata
/// and comparing on join is the next step, and that one cannot be verified without two machines
/// in a session — see <c>docs/features/coop-interop.md</c> "Verification status".
/// <see cref="SettingsFingerprint.FingerprintReport.DivergentGroups"/> is already there for it.</para>
///
/// <para>Kept out of <c>SubModule.cs</c> per ADR-002 (thin entry points): the call site is one
/// line inside the existing co-op-gated block.</para>
/// </remarks>
public static class SettingsFingerprintLog
{
    public const string Tag = "[SettingsFingerprint]";

    /// <summary>
    /// Log the global code and one line per group. Never throws: a diagnostic that can take the
    /// session down is worse than no diagnostic.
    /// </summary>
    public static void Write(object settings, IModLogger logger)
    {
        if (settings == null || logger == null) return;
        WriteAcross(logger, settings);
    }

    /// <summary>
    /// Log the fingerprint taken across every settings class TAOM ships. Never throws, for the
    /// reason above. Logger first because the settings list is variadic.
    /// </summary>
    public static void WriteAcross(IModLogger logger, params object[] settingsObjects)
    {
        if (logger == null || settingsObjects == null) return;
        try
        {
            var report = SettingsFingerprint.ComputeAcross(settingsObjects);
            logger.LogInfo(
                $"{Tag} global={report.ShortGlobal} over {report.Covered} simulation-relevant setting(s). " +
                "Compare this line with the other peer's — a difference means the two campaigns " +
                "will compute different outcomes, and nothing else will say so.");

            foreach (var kv in report.ByGroup.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                var count = report.CountsByGroup.TryGetValue(kv.Key, out var c) ? c : 0;
                logger.LogInfo($"{Tag}   {kv.Key} = {Shorten(kv.Value)} ({count})");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning($"{Tag} could not be computed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string Shorten(string hash) =>
        string.IsNullOrEmpty(hash) ? "?" : hash.Substring(0, Math.Min(12, hash.Length));
}
