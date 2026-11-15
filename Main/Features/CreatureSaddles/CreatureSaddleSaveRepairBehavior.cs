using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.CreatureSaddles;

/// <summary>
/// Old saves (docs/features/creature-saddles.md): a save made before Patch154 can hold a horse saddle on a warg, dire
/// wolf or spider (or a creature saddle on a horse) in a player clan hero's equipment. At session launch each such
/// harness moves off the mount into the main party's inventory, so the hero rides bare instead of carrying a harness the
/// inventory would now refuse. Only the player clan's heroes: AI heroes take kits from templates, and Patch153 keeps
/// the cloth reins off any mount without a rein skeleton for everyone. No save data.
/// </summary>
public sealed class CreatureSaddleSaveRepairBehavior : CampaignBehaviorBase
{
    private readonly IModLogger _logger;
    private readonly ICoopSessionProvider _coop;

    public CreatureSaddleSaveRepairBehavior(IModLogger logger, ICoopSessionProvider coop)
    {
        _logger = logger;
        _coop = coop;
    }

    public override void RegisterEvents() =>
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);

    public override void SyncData(IDataStore dataStore) { }

    private void OnSessionLaunched(CampaignGameStarter _)
    {
        // Co-op: equipment and the party inventory are saved state; only the host writes them.
        if (!_coop.IsAuthority || !CoopSessionPolicy.MayWriteSaveBackedState(_coop.IsCoopClient))
            return;

        try
        {
            var clan = Clan.PlayerClan;
            var inventory = MobileParty.MainParty?.ItemRoster;
            if (clan == null || inventory == null)
                return;

            int moved = 0;
            foreach (var hero in clan.Heroes.Concat(clan.Companions).Distinct())
            {
                if (hero == null || hero.IsDead)
                    continue;
                moved += Repair(hero, hero.BattleEquipment, inventory, "battle");
                moved += Repair(hero, hero.CivilianEquipment, inventory, "civilian");
            }

            if (moved > 0)
                _logger.LogInfo($"[CreatureSaddles] moved {moved} harness(es) that no longer fit their mount to the party inventory");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[CreatureSaddles] old-save harness check failed ({ex.GetType().Name}): equipment left as loaded");
        }
    }

    private int Repair(Hero hero, Equipment? equipment, ItemRoster inventory, string set)
    {
        if (equipment == null)
            return 0;
        var mount = equipment[EquipmentIndex.Horse];
        var harness = equipment[EquipmentIndex.HorseHarness];
        var monster = mount.Item?.HorseComponent?.Monster;
        var armor = harness.Item?.ArmorComponent;
        if (mount.IsEmpty || harness.IsEmpty || monster == null || armor == null)
            return 0;
        if (!CreatureSaddleFamilies.MustUnequipHarness(monster.FamilyType, armor.FamilyType))
            return 0;

        equipment[EquipmentIndex.HorseHarness] = EquipmentElement.Invalid;
        inventory.AddToCounts(harness, 1);
        _logger.LogInfo($"[CreatureSaddles] {hero.StringId} ({set}): '{harness.Item!.StringId}' (family {armor.FamilyType}) taken off '{mount.Item!.StringId}' (family {monster.FamilyType}) into the party inventory");
        return 1;
    }
}
