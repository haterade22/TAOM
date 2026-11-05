using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.TroopWeight.Diagnostics;

/// <summary>
/// TEMPORARY, pure formatting for the main-party roster loss diagnostic (player report 2026-10-17:
/// about 60 non-hero troops vanished after an Elite Emissary purchase). Stateless and free of engine
/// types: it holds no counters and never touches a roster, so it is safe from any thread. The reader
/// groups the log lines by <c>tick=</c>, the value of <c>Campaign.CurrentTickCount</c>, which advances
/// once per map frame while the map is active, so one tick is one map frame, one whole party-screen
/// session or one whole mission. The removal checklist is in the <c>Patch_MainRosterLossDiag</c>
/// entry of docs/reference/harmony-patch-registry.md.
/// </summary>
public static class MainRosterLossDiagnostics
{
    public const string Prefix = "[RosterLoss]";

    private const int MaxChainFrames = 8;

    private static readonly string[] NoisePrefixes =
    {
        "TAOM.Features.TroopWeight.Diagnostics.",
        "HarmonyLib.",
        "MonoMod.",
        "System.",
    };

    public static string FormatRemoval(
        int tick, string troopId, int removed, int rowNumber, int regulars, IReadOnlyList<string> frames)
    {
        string chain = string.Join(" <- ", FormatCallerChain(frames));
        if (chain.Length == 0)
            chain = "(no managed caller)";

        return $"{Prefix} tick={tick} -{removed} {troopId} " +
               $"(row {rowNumber} -> {rowNumber - removed}, regulars {regulars} -> {regulars - removed}) via {chain}";
    }

    public static IReadOnlyList<string> FormatCallerChain(IReadOnlyList<string> frames)
    {
        if (frames == null)
            return new List<string>();

        return frames
            .Where(f => !string.IsNullOrEmpty(f))
            .Where(f => !NoisePrefixes.Any(p => f.StartsWith(p, StringComparison.Ordinal)))
            .Take(MaxChainFrames)
            .ToList();
    }
}
