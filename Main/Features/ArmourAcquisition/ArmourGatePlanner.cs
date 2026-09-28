using System;
using System.Collections.Generic;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Features.ArmourAcquisition;

/// <summary>
/// Decides, for one game's loaded items, which are governed, each one's class and which gated pieces
/// must stop being merchandise. Pure. Governed means character armour (every piece, the vanilla ones
/// included, so no market stocks a heavy Calradian piece either) and the named weapons. The named list
/// wins over the table; the table wins over the engine tier; a piece already non-merchandise in its XML
/// is classed but never flipped.
/// </summary>
public static class ArmourGatePlanner
{
    public static ArmourGatePlan Plan(
        IReadOnlyList<ArmourItemRecord> items,
        IReadOnlyDictionary<string, ArmourClassEntry> table,
        IReadOnlyCollection<string> namedWeapons,
        bool enabled)
    {
        var named = new HashSet<string>(namedWeapons, StringComparer.Ordinal);
        var classById = new Dictionary<string, ArmourClass>(StringComparer.Ordinal);
        var toFlip = new List<string>();
        var loaded = new HashSet<string>(StringComparer.Ordinal);
        int fromTable = 0, fromTier = 0;

        foreach (var item in items)
        {
            if (string.IsNullOrEmpty(item.ItemId))
                continue;
            loaded.Add(item.ItemId);

            ArmourClass cls;
            if (named.Contains(item.ItemId))
                cls = ArmourClass.Named;
            else if (table.TryGetValue(item.ItemId, out var entry))
            {
                cls = entry.Class;
                fromTable++;
            }
            else if (item.IsCharacterArmour)
            {
                cls = ArmourClassRules.FromEngineTier(item.EngineTier);
                fromTier++;
            }
            else
                continue;

            classById[item.ItemId] = cls;
            if (enabled && item.IsMerchandise && ArmourClassRules.IsGated(cls))
                toFlip.Add(item.ItemId);
        }

        var stale = new List<string>();
        foreach (var id in table.Keys)
            if (!loaded.Contains(id))
                stale.Add(id);
        stale.Sort(StringComparer.Ordinal);

        var missingNamed = new List<string>();
        foreach (var id in named)
            if (!loaded.Contains(id))
                missingNamed.Add(id);
        missingNamed.Sort(StringComparer.Ordinal);

        return new ArmourGatePlan(classById, toFlip, fromTable, fromTier, stale, missingNamed);
    }
}
