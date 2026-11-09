using System;

namespace TAOM.Features.AdvancedCombat;

/// <summary>
/// Which mounts are TAOM creatures rather than vanilla's four plain mounts. The engine's native mounted-death
/// path (<c>MBAPI.IMBAgent.Die</c>) faulted for the spider (debugger-proven on 1.4.6, docs/features/spider.md).
/// A v2.0.29.5 player CTD (2026-11-07) died in the same call from a warg bite in a battle of wargs and war rams;
/// its victim was not logged. Patch47 routes every creature mount around the mounted branches rather than wait
/// for a second proof. Patch48 uses the same predicate, so a hit cannot knock a creature rider off.
/// Ask it only about a mount's Monster id: a race id such as "human" also returns true.
/// </summary>
public static class CreatureMounts
{
    private static readonly string[] VanillaMounts = { "horse", "horse_2", "camel", "mule" };

    /// <summary>For a mount's Monster id: true outside vanilla's plain mounts, false for those and for no id.</summary>
    public static bool IsCreatureMount(string? monsterId) =>
        !string.IsNullOrEmpty(monsterId) && Array.IndexOf(VanillaMounts, monsterId) < 0;
}
