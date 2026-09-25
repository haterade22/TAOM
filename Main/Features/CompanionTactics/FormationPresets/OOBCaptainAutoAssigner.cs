using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CompanionTactics.FormationPresets.Models;

namespace TAOM.Features.CompanionTactics.FormationPresets;

/// <summary>
/// Boundary class for the OOB Auto-Assign button. Reads the live vanilla view model, adapts each
/// candidate hero for <see cref="IHeroAutoAssigner.PlanCaptains"/>, and applies the plan through
/// vanilla's own manual-drag path (select the hero, then the formation's accept-captain
/// command), so vanilla keeps every side effect: agent formation, Formation.Captain, banner,
/// unassigned list and the tutorial event. Uses public members only; no reflection.
/// Keeps every captain already placed and never places the player's own hero.
/// </summary>
public sealed class OOBCaptainAutoAssigner : IOOBCaptainAutoAssigner
{
    private readonly IHeroAutoAssigner _planner;
    private readonly ICompanionTacticsSettingsProvider _settings;
    private readonly IModLogger _logger;

    public OOBCaptainAutoAssigner(
        IHeroAutoAssigner planner,
        ICompanionTacticsSettingsProvider settings,
        IModLogger logger)
    {
        _planner = planner;
        _settings = settings;
        _logger = logger;
    }

    public AutoAssignResult AssignCaptains(OrderOfBattleVM vm)
    {
        if (vm == null) return new AutoAssignResult(AutoAssignStatus.NoneAssigned, 0);
        if (!vm.IsPlayerGeneral) return new AutoAssignResult(AutoAssignStatus.NotGeneral, 0);

        var formations = new List<OrderOfBattleFormationItemVM>();
        AddAll(formations, vm.FormationsFirstHalf);
        AddAll(formations, vm.FormationsSecondHalf);

        var slots = new List<OrderOfBattleFormationItemVM>();
        var slotClasses = new List<int>();
        foreach (var formation in formations)
        {
            if (!formation.HasFormation || formation.HasCaptain) continue;
            slots.Add(formation);
            slotClasses.Add((int)formation.GetOrderOfBattleClass());
        }

        var heroItems = new List<OrderOfBattleHeroItemVM>();
        var heroes = new List<IHeroCombatAdapter>();
        if (vm.UnassignedHeroes != null)
            foreach (var item in vm.UnassignedHeroes) AddCandidate(item, heroItems, heroes);
        foreach (var formation in formations)
        {
            if (formation.HeroTroops == null) continue;
            foreach (var item in formation.HeroTroops) AddCandidate(item, heroItems, heroes);
        }

        var assigned = 0;
        foreach (var pick in _planner.PlanCaptains(heroes, slotClasses))
        {
            var hero = heroItems[pick.HeroIndex];
            var slot = slots[pick.SlotIndex];
            vm.ExecuteClearHeroSelection();
            OrderOfBattleHeroItemVM.OnHeroSelection?.Invoke(hero);
            slot.ExecuteAcceptCaptain();
            if (slot.Captain == hero) assigned++;
            else _logger.LogWarning($"[FormationPresets] Auto-Assign: vanilla did not accept {hero.Agent?.Name} as captain of formation {slot.Formation?.Index}");
        }
        vm.ExecuteClearHeroSelection();

        if (_settings.FormationPresetsDebug)
            _logger.LogDebug($"[FormationPresets] Auto-Assign: {heroes.Count} candidates, {slots.Count} open slots, {assigned} captains placed");
        return assigned > 0
            ? new AutoAssignResult(AutoAssignStatus.Assigned, assigned)
            : new AutoAssignResult(AutoAssignStatus.NoneAssigned, 0);
    }

    private static void AddAll(List<OrderOfBattleFormationItemVM> into, IEnumerable<OrderOfBattleFormationItemVM> from)
    {
        if (from == null) return;
        foreach (var formation in from)
            if (formation != null) into.Add(formation);
    }

    private static void AddCandidate(OrderOfBattleHeroItemVM item,
        List<OrderOfBattleHeroItemVM> items, List<IHeroCombatAdapter> heroes)
    {
        if (item == null || item.IsMainHero || item.IsLeadingAFormation || item.IsDisabled) return;
        if (items.Contains(item)) return;
        var hero = ResolveAgentHero(item.Agent);
        if (hero == null) return;
        items.Add(item);
        // Classify from what the agent spawned with, not the campaign BattleEquipment: in a siege
        // assault vanilla spawns every agent without a horse, so a companion who owns one fights
        // on foot and must be able to lead a foot formation (the only classes a siege offers).
        heroes.Add(new HeroCombatAdapter(hero, item.Agent.SpawnEquipment ?? hero.BattleEquipment));
    }

    // Same resolution as RoleTooltipDecorator.ResolveAgentHero. Outside sieges the spawn
    // equipment is a clone of the hero's BattleEquipment, so the role matches the OOB badge.
    private static Hero ResolveAgentHero(Agent agent)
    {
        if (agent == null) return null;
        if (agent.Character is not CharacterObject co || !co.IsHero) return null;
        var hero = co.HeroObject;
        if (hero == null || hero == Hero.MainHero) return null;
        return hero;
    }
}
