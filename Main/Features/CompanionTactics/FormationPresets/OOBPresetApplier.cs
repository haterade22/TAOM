using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
using TAOM.Core.Logging;
using TAOM.Features.CompanionTactics.FormationPresets.Models;

namespace TAOM.Features.CompanionTactics.FormationPresets;

/// <summary>
/// Boundary class for formation presets. Capture reads the live vanilla view model into plain snapshots for
/// <see cref="FormationPresetLayout"/>. Apply drives vanilla's own public flows, so vanilla keeps every side effect.
/// A hero goes through vanilla's click path (select the hero, then the formation's accept command), the same one
/// <see cref="OOBCaptainAutoAssigner"/> takes, not a drag. A class change goes through the class selector like the
/// player's dropdown pick, so vanilla moves troops only as it does for that pick: all troops of the class come in only
/// when no other formation has the class, and a class that another formation already has starts at 0 percent. A preset
/// stores no troop shares or filters. Public members only; no reflection. Each step is verified, because vanilla
/// refuses silently. A hero the preset does not mention gets no step of its own, but a saved captain displaces the
/// formation's current captain to the unassigned list, as a manual pick does.
/// </summary>
public sealed class OOBPresetApplier : IOOBPresetApplier
{
    private readonly ICompanionTacticsSettingsProvider _settings;
    private readonly IModLogger _logger;

    public OOBPresetApplier(ICompanionTacticsSettingsProvider settings, IModLogger logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public HoNFormationPreset Capture(OrderOfBattleVM vm, string name)
    {
        var snapshots = new List<PresetFormationSnapshot>();
        foreach (var formation in FormationsByIndex(vm).Values)
        {
            var captainId = formation.HasCaptain ? HeroId(formation.Captain?.Agent) : null;
            var troopIds = new List<string>();
            if (formation.HeroTroops != null)
            {
                foreach (var troop in formation.HeroTroops)
                {
                    var id = HeroId(troop?.Agent);
                    if (id != null) troopIds.Add(id);
                }
            }
            snapshots.Add(new PresetFormationSnapshot(formation.Formation.Index, (int)formation.GetOrderOfBattleClass(),
                captainId, troopIds));
        }
        return FormationPresetLayout.Capture(name, snapshots);
    }

    public PresetApplyResult Apply(OrderOfBattleVM vm, HoNFormationPreset preset)
    {
        // Vanilla's accept-captain path does extra work for a non-general (assigns the player's role), so a
        // non-general never gets this far; the overlay VM gates the menu on the same condition.
        if (vm == null || preset == null || !vm.IsPlayerGeneral) return new PresetApplyResult(0, 0, 0, 0);

        var formations = FormationsByIndex(vm);
        var offered = formations.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<int>)OfferedClasses(pair.Value));
        var heroItems = HeroItemsById(vm, formations.Values);

        var plan = FormationPresetLayout.PlanLoad(preset, offered,
            step => TryApplyClass(formations[step.FormationIndex], step.Class), new HashSet<string>(heroItems.Keys));

        var skipped = plan.Skipped;
        var captains = 0;
        foreach (var step in plan.Heroes.Captains)
        {
            if (PlaceCaptain(vm, formations[step.FormationIndex], heroItems[step.HeroId])) captains++;
            else skipped++;
        }
        var troops = 0;
        foreach (var step in plan.Heroes.Troops)
        {
            if (PlaceHeroTroop(vm, formations[step.FormationIndex], heroItems[step.HeroId])) troops++;
            else skipped++;
        }
        vm.ExecuteClearHeroSelection();

        if (_settings.FormationPresetsDebug)
        {
            foreach (var stuck in plan.Classes.Stuck)
                _logger.LogDebug($"[FormationPresets] Load: formation {stuck.FormationIndex} cannot take class {stuck.Class} now");
            _logger.LogDebug($"[FormationPresets] Load \"{preset.Name}\": {plan.Classes.Applied.Count} classes, {captains} captains, {troops} hero troops, {skipped} skipped");
        }
        return new PresetApplyResult(plan.Classes.Applied.Count, captains, troops, skipped);
    }

    // False when the formation cannot take the change right now (IsAdjustable also requires the player to control it);
    // the pass loop retries it after other formations changed. A formation that already has the class counts as set.
    private bool TryApplyClass(OrderOfBattleFormationItemVM formation, int targetClass)
    {
        if ((int)formation.GetOrderOfBattleClass() == targetClass) return true;
        if (!formation.IsAdjustable) return false;

        var index = OfferedClasses(formation).IndexOf(targetClass);
        if (index < 0) return false;

        formation.FormationClassSelector.SelectedIndex = index;
        if ((int)formation.GetOrderOfBattleClass() == targetClass) return true;
        _logger.LogWarning($"[FormationPresets] Load: vanilla did not change formation {formation.Formation.Index} to class {targetClass}");
        return false;
    }

    private bool PlaceCaptain(OrderOfBattleVM vm, OrderOfBattleFormationItemVM formation, OrderOfBattleHeroItemVM hero)
    {
        if (formation.HasCaptain && formation.Captain == hero) return true;
        vm.ExecuteClearHeroSelection();
        OrderOfBattleHeroItemVM.OnHeroSelection?.Invoke(hero);
        formation.ExecuteAcceptCaptain();
        if (formation.HasCaptain && formation.Captain == hero) return true;
        _logger.LogWarning($"[FormationPresets] Load: vanilla did not accept {hero.Agent?.Name} as captain of formation {formation.Formation.Index}");
        return false;
    }

    private bool PlaceHeroTroop(OrderOfBattleVM vm, OrderOfBattleFormationItemVM formation, OrderOfBattleHeroItemVM hero)
    {
        if (formation.HeroTroops.Contains(hero)) return true;
        vm.ExecuteClearHeroSelection();
        OrderOfBattleHeroItemVM.OnHeroSelection?.Invoke(hero);
        formation.ExecuteAcceptHeroTroops();
        if (formation.HeroTroops.Contains(hero)) return true;
        _logger.LogWarning($"[FormationPresets] Load: vanilla did not accept {hero.Agent?.Name} as a hero troop of formation {formation.Formation.Index}");
        return false;
    }

    private static SortedDictionary<int, OrderOfBattleFormationItemVM> FormationsByIndex(OrderOfBattleVM vm)
    {
        var byIndex = new SortedDictionary<int, OrderOfBattleFormationItemVM>();
        AddFormations(byIndex, vm.FormationsFirstHalf);
        AddFormations(byIndex, vm.FormationsSecondHalf);
        return byIndex;
    }

    private static void AddFormations(IDictionary<int, OrderOfBattleFormationItemVM> into, IEnumerable<OrderOfBattleFormationItemVM> from)
    {
        if (from == null) return;
        foreach (var formation in from)
            if (formation?.Formation != null) into[formation.Formation.Index] = formation;
    }

    private static List<int> OfferedClasses(OrderOfBattleFormationItemVM formation) =>
        formation.FormationClassSelector.ItemList.Select(item => (int)item.FormationClass).ToList();

    private static Dictionary<string, OrderOfBattleHeroItemVM> HeroItemsById(
        OrderOfBattleVM vm, IEnumerable<OrderOfBattleFormationItemVM> formations)
    {
        var byId = new Dictionary<string, OrderOfBattleHeroItemVM>();
        if (vm.UnassignedHeroes != null)
            foreach (var item in vm.UnassignedHeroes) Add(byId, item);
        foreach (var formation in formations)
        {
            if (formation.HasCaptain) Add(byId, formation.Captain);
            if (formation.HeroTroops == null) continue;
            foreach (var item in formation.HeroTroops) Add(byId, item);
        }
        return byId;
    }

    private static void Add(Dictionary<string, OrderOfBattleHeroItemVM> byId, OrderOfBattleHeroItemVM item)
    {
        var id = HeroId(item?.Agent);
        if (id != null && !byId.ContainsKey(id)) byId[id] = item;
    }

    // The hero's StringId, or null for an agent that is not a hero (the captain slot's empty placeholder has no agent).
    private static string HeroId(Agent agent) =>
        agent?.Character is CharacterObject character && character.IsHero ? character.StringId : null;
}
