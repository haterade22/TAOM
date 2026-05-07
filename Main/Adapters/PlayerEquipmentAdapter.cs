using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace TAOM.Adapters;

public sealed class PlayerEquipmentAdapter : IPlayerEquipmentAdapter
{
    public int TryUnequipAllPlayerSlots()
    {
        var hero = Hero.MainHero;
        if (hero == null) return 0;
        var party = PartyBase.MainParty;
        var roster = party?.ItemRoster;
        if (roster == null) return 0;

        var deadBattle = Campaign.Current?.DeadBattleEquipment;
        var deadCivilian = Campaign.Current?.DeadCivilianEquipment;

        int count = 0;
        count += StripEquipment(hero.BattleEquipment, deadBattle, roster);
        count += StripEquipment(hero.CivilianEquipment, deadCivilian, roster);
        return count;
    }

    private static int StripEquipment(Equipment equipment, Equipment deadSingleton, ItemRoster roster)
    {
        if (equipment == null || equipment == deadSingleton) return 0;
        int count = 0;
        // Iterate every meaningful slot 0..11: Weapon0..3 + ExtraWeaponSlot (0..4),
        // Head/Body/Leg/Gloves/Cape (5..9), Horse (10), HorseHarness (11).
        // Deep-review #36 caught earlier version stopping at ArmorItemEndSlot=10 (exclusive)
        // which left Horse + HorseHarness equipped — UI promise "Unequip All" must match code.
        for (var i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.NumEquipmentSetSlots; i++)
        {
            var element = equipment[i];
            if (element.IsEmpty) continue;
            // Modifier-preserving overload — keeps ItemModifier on quality/durability variants.
            roster.AddToCounts(element, 1);
            equipment[i] = EquipmentElement.Invalid;
            count++;
        }
        return count;
    }

    public PlayerEquipmentApplyResult ApplyRosterToPlayer(string rosterId, string playerHeroId)
    {
        var roster = MBObjectManager.Instance?.GetObject<MBEquipmentRoster>(rosterId);
        if (roster == null)
            return PlayerEquipmentApplyResult.RosterNotFound;

        var battle = roster.AllEquipments.FirstOrDefault(e => e.IsBattle);
        var civilian = roster.AllEquipments.FirstOrDefault(e => e.IsCivilian);
        if (battle == null && civilian == null)
            return PlayerEquipmentApplyResult.NoSuitableEquipment;

        var hero = Hero.FindFirst(h => h.StringId == playerHeroId);
        if (hero == null)
            return PlayerEquipmentApplyResult.HeroNotFound;

        // Hero.BattleEquipment falls through to Campaign.Current.DeadBattleEquipment and
        // Hero.CivilianEquipment falls through to Campaign.Current.DeadCivilianEquipment (two
        // separate process-wide singletons) when the hero's _battleEquipment / _civilianEquipment
        // is null. Calling FillFrom on those fallbacks would corrupt equipment for every
        // dead/uninitialized hero. MainHero at CC finalize is always initialized, but the
        // adapter takes any heroId — guard each slot against its OWN singleton.
        // Codex review caught the original deep-review fix here comparing both against
        // DeadBattleEquipment (2026-05-06).
        var deadBattle = Campaign.Current?.DeadBattleEquipment;
        var deadCivilian = Campaign.Current?.DeadCivilianEquipment;
        if (battle != null && hero.BattleEquipment != null && hero.BattleEquipment != deadBattle)
            hero.BattleEquipment.FillFrom(battle);
        if (civilian != null && hero.CivilianEquipment != null && hero.CivilianEquipment != deadCivilian)
            hero.CivilianEquipment.FillFrom(civilian);

        return PlayerEquipmentApplyResult.Success;
    }
}
