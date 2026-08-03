using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.BattleLoadDiagnostics.Domain;

namespace TAOM.Adapters;

public sealed class EquipmentSnapshotAdapter : IEquipmentSnapshotAdapter
{
    // Engine slot order 0..11 (EquipmentIndex). Explicit names avoid the duplicate-value
    // ambiguity of EquipmentIndex.ToString() (Weapon0 and WeaponItemBeginSlot are both 0).
    private static readonly string[] SlotNames =
    {
        "Weapon0", "Weapon1", "Weapon2", "Weapon3", "ExtraWeapon",
        "Head", "Body", "Leg", "Gloves", "Cape", "Horse", "HorseHarness"
    };

    public EquipmentSnapshot? Capture(object agent, string? spawnOrigin = null)
    {
        if (!(agent is Agent a)) return null;

        var slots = new List<EquipmentSlotSnapshot>();
        // SpawnEquipment is the FULL Equipment (weapons + armor + horse) — not Agent.Equipment
        // (MissionEquipment, weapon slots only), which would miss the shield/armor whose
        // shield_body_name is the prime hang suspect.
        var spawn = a.SpawnEquipment;
        if (spawn != null)
        {
            for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots && i < SlotNames.Length; i++)
            {
                var element = spawn[(EquipmentIndex)i];
                if (element.IsEmpty) continue;
                var item = element.Item;
                if (item == null) continue;

                slots.Add(new EquipmentSlotSnapshot(
                    SlotNames[i],
                    item.StringId ?? "<noid>",
                    item.BodyName,
                    item.CollisionBodyName,
                    item.HolsterBodyName,
                    item.MultiMeshName,
                    item.ItemType.ToString()));
            }
        }

        var character = a.Character as CharacterObject;
        string charId = character?.StringId ?? a.Character?.StringId ?? "<nochar>";
        string cultureId = character?.Culture?.StringId ?? "<noculture>";

        return new EquipmentSnapshot(
            a.Index, a.Name ?? "<unnamed>", charId, cultureId, slots,
            RaceOf(a), MonsterOf(a), ActionSetOf(a), spawnOrigin);
    }

    // Each identity getter is guarded independently: Agent.ActionSet dereferences the native
    // pointer (Agent.cs:696 -> MBAPI.IMBAgent.GetActionSetNo) and Monster/Race read engine state
    // that can be absent on a partially-built agent. One of them failing must not blank the
    // others, and none of them may cost us the Begin line — which is the only durable proof the
    // agent existed at all when the process dies moments later.
    private static string? RaceOf(Agent a)
    {
        try
        {
            int race = a.Character?.Race ?? -1;
            if (race < 0) return null;

            // NOT GetRaceNames(): that returns `(string[])_raceNamesArray.Clone()`
            // (MountAndBlade/FaceGen.cs:125) — a fresh 15-element array on EVERY call, which on
            // this path is one allocation per agent built (648 observed in a single arena load)
            // to read one element. GetBaseMonsterNameFromRace indexes the same array directly
            // (:120) and allocates nothing. GetRaceCount() returns 0 when FaceGen has no
            // instance, so the bounds check also covers the uninitialised case.
            // Fully qualified because TaleWorlds.MountAndBlade declares a FaceGen too; only the
            // Core one exposes these as statics.
            if (race >= TaleWorlds.Core.FaceGen.GetRaceCount()) return race.ToString();
            var name = TaleWorlds.Core.FaceGen.GetBaseMonsterNameFromRace(race);
            return string.IsNullOrEmpty(name) ? race.ToString() : name;
        }
        catch { return null; }
    }

    private static string? MonsterOf(Agent a)
    {
        try { return a.Monster?.StringId; }
        catch { return null; }
    }

    private static string? ActionSetOf(Agent a)
    {
        try
        {
            var set = a.ActionSet;
            if (!set.IsValid) return "<invalid>";
            return set.GetName();
        }
        catch { return null; }
    }
}
