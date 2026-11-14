using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>One spawn of an upgrade order (#1161): whether the new soldier is active, how far he stands from the old soldier's place, and both places as text for the log.</summary>
internal readonly record struct UpgradeSpawn(bool Active, float Moved, string Where);

/// <summary>The report of an upgrade order from its spawns (#1161, Codex F1): only an active soldier is replaced and paid for, and only one within the radius reads back.</summary>
internal static class UpgradeTally
{
    public static LdUpgradeReport Of(int found, IReadOnlyList<UpgradeSpawn> spawns, float radius)
    {
        int replaced = 0, readBack = 0;
        string? miss = null;
        foreach (var spawn in spawns)
        {
            if (!spawn.Active)
                continue;
            replaced++;
            if (spawn.Moved <= radius)
                readBack++;
            else
                miss ??= spawn.Where;
        }

        return new LdUpgradeReport(found, replaced, readBack, null, miss);
    }
}
