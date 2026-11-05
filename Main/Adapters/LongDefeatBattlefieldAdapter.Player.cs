using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.LongDefeat.Domain;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace TAOM.Adapters;

/// <summary>The player's lord: his spawn at the start and at a respawn, the class kit he carries into it, and the read-back of what the engine made.</summary>
internal sealed partial class LongDefeatBattlefieldAdapter
{
    private static readonly LdKitFacts NoLord = new(false, Array.Empty<LdKitSlot>(), Array.Empty<string>(), false);

    // The facts are read once, in the frame of the spawn, and kept in place of the Agent: an Agent slot recycles, and a held one would answer IsActive for whoever fills it next.
    private LdKitFacts _kitFacts = NoLord;
    private List<string> _kitSkipped = new();

    public bool SpawnPlayer(bool respawn, LdLoadout? loadout)
    {
        var player = Game.Current?.PlayerTroop;
        var team = _mission.DefenderTeam;
        if (player == null || team == null)
            return false;

        var kit = loadout == null ? null : BuildKit(player, loadout);
        if (respawn)
        {
            var formation = team.GetFormation(FormationClass.Infantry);
            formation.BeginSpawn(1, false);
            var respawned = Spawn(player, team, true, FormationClass.Infantry, true, 1, 0, true, kit: kit);
            formation.EndSpawn();
            _kitFacts = ReadBack(respawned, loadout);
            return true;
        }

        var count = _open.TryGetValue((false, FormationSlot.Infantry), out var started) ? started : 1;
        _kitFacts = ReadBack(Spawn(player, team, true, FormationClass.Infantry, false, count, count - 1, false, kit: kit), loadout);
        return true;
    }

    // The kit is a copy of the lord's first battle set with the loadout's slots overwritten; with no override the engine picks his set itself (Mission.cs:4108 takes this one first).
    // The XML slot names go through the engine's own mapping, because Item0 is not a member of EquipmentIndex. A slot the engine cannot take (an unknown item, a misfit, a horse that
    // cannot be ridden) is left as the lord had it and named, so the class check fails instead of the run quietly playing another kit. A troop with no battle set starts from an empty one:
    // spawned without the override, a hero would throw on FirstBattleEquipment.Clone() (Mission.cs:4284-4287, :4108) and any other troop would get an empty set (Equipment.cs:562).
    private Equipment BuildKit(BasicCharacterObject troop, LdLoadout loadout)
    {
        _kitSkipped = new List<string>();
        var kit = troop.FirstBattleEquipment?.Clone() ?? new Equipment(Equipment.EquipmentType.Battle);
        foreach (var slot in loadout.Slots)
        {
            if (!TryIndex(slot.Slot, out var index))
            {
                _kitSkipped.Add(slot.Slot);
                continue;
            }

            if (slot.ItemId.Length == 0)
            {
                kit[index] = default;
                continue;
            }

            var item = MBObjectManager.Instance.GetObject<ItemObject>(slot.ItemId);
            var rideable = index != EquipmentIndex.Horse || item?.HorseComponent?.IsRideable == true;
            if (item == null || !rideable || !Equipment.IsItemFitsToSlot(index, item))
            {
                _kitSkipped.Add(slot.Slot);
                continue;
            }

            kit[index] = new EquipmentElement(item);
        }

        if (_kitSkipped.Count > 0)
            _logger.LogWarning($"[LongDefeat][Class] the {loadout.ClassId} kit could not fill {string.Join(", ", _kitSkipped)} (an unknown item, or one that does not fit the slot); the lord keeps his own there");
        return kit;
    }

    private static bool TryIndex(string slot, out EquipmentIndex index)
    {
        try
        {
            index = Equipment.GetEquipmentIndexFromOldEquipmentIndexName(slot);
            return true;
        }
        catch (ArgumentException)
        {
            index = EquipmentIndex.None;
            return false;
        }
    }

    public LdKitFacts ReadKit() => _kitFacts;

    // Read from the body the engine built, never from the kit above: the check must see what the spawn made. SpawnEquipment is set at the spawn (Mission.cs:4303) after every engine
    // edit of the override, so it also shows a slot the engine cleared itself.
    private LdKitFacts ReadBack(Agent? lord, LdLoadout? loadout)
    {
        if (lord == null || loadout == null || !lord.IsActive())
            return NoLord;

        var found = new List<LdKitSlot>();
        foreach (var slot in loadout.Slots)
            found.Add(new LdKitSlot(slot.Slot, TryIndex(slot.Slot, out var index) ? lord.SpawnEquipment[index].Item?.StringId ?? "" : ""));
        return new LdKitFacts(true, found, _kitSkipped.ToList(), lord.HasMount);
    }
}
