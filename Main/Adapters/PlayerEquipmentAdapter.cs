using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace TAOM.Adapters;

public sealed class PlayerEquipmentAdapter : IPlayerEquipmentAdapter
{
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
