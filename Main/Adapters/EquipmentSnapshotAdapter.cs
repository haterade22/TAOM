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

    public EquipmentSnapshot? Capture(object agent)
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

        return new EquipmentSnapshot(a.Index, a.Name ?? "<unnamed>", charId, cultureId, slots);
    }
}
