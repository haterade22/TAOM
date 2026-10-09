using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.CompanionTactics.FormationPresets.Models;

namespace TAOM.Features.CompanionTactics.FormationPresets;

/// <summary>One formation of the Order of Battle as plain data. <c>Class</c> is the vanilla DeploymentFormationClass value (0 = Unset).</summary>
public sealed record PresetFormationSnapshot(
    int Index, int Class, string CaptainId, IReadOnlyList<string> TroopIds);

public sealed record ClassStep(int FormationIndex, int Class);

public sealed record HeroStep(string HeroId, int FormationIndex);

public sealed record ClassPlan(IReadOnlyList<ClassStep> Steps, int Skipped);

public sealed record HeroPlan(IReadOnlyList<HeroStep> Captains, IReadOnlyList<HeroStep> Troops, int Skipped);

/// <summary>The class steps that took effect (applied now or already in place) and those no pass could apply.</summary>
public sealed record PassResult(IReadOnlyList<ClassStep> Applied, IReadOnlyList<ClassStep> Stuck);

public sealed record LoadPlan(PassResult Classes, HeroPlan Heroes, int Skipped);

public sealed record PresetCounts(int Classes, int Captains, int Troops);

/// <summary>
/// The pure mapping between an Order of Battle layout and a <see cref="HoNFormationPreset"/>: what Save
/// captures, what Load plans, and the pass loop for class changes. No TaleWorlds types; the boundary
/// (<see cref="OOBPresetApplier"/>) reads the live view model into snapshots and drives vanilla with the plan.
/// </summary>
public static class FormationPresetLayout
{
    // Vanilla's own campaign load (SPOrderOfBattleVM.LoadConfiguration) maps the mounted classes a siege
    // does not offer: HorseArcher to Ranged, Cavalry to Infantry, CavalryAndHorseArcher to InfantryAndRanged.
    private static readonly Dictionary<int, int> SiegeMapping = new() { [4] = 2, [3] = 1, [6] = 5 };

    // Vanilla's SaveConfiguration decides "unset" from the class alone, and keeps a formation's flag and class in step.
    public static HoNFormationPreset Capture(string name, IEnumerable<PresetFormationSnapshot> formations)
    {
        var preset = new HoNFormationPreset(name);
        foreach (var formation in formations)
        {
            preset.FormationClasses[formation.Index] = formation.Class > 0 ? formation.Class : -1;
            if (!string.IsNullOrEmpty(formation.CaptainId))
            {
                preset.HeroFormationAssignments[formation.CaptainId] = formation.Index;
                if (!preset.IsCaptain(formation.CaptainId)) preset.CaptainHeroIds.Add(formation.CaptainId);
            }
            foreach (var troopId in formation.TroopIds)
                preset.HeroFormationAssignments[troopId] = formation.Index;
        }
        return preset;
    }

    public static PresetCounts CountOf(HoNFormationPreset preset)
    {
        var classes = preset.FormationClasses.Count(pair => pair.Value > 0);
        var captains = preset.CaptainHeroIds.Count;
        var troops = preset.HeroFormationAssignments.Keys.Count(id => !preset.IsCaptain(id));
        return new PresetCounts(classes, captains, troops);
    }

    /// <summary>The saved class when the formation offers it, else the siege mapping when that is offered, else none.</summary>
    public static int? ResolveClass(int saved, IReadOnlyCollection<int> offered)
    {
        if (saved <= 0) return null;
        if (offered.Contains(saved)) return saved;
        if (SiegeMapping.TryGetValue(saved, out var mapped) && offered.Contains(mapped)) return mapped;
        return null;
    }

    public static ClassPlan PlanClasses(HoNFormationPreset preset, IReadOnlyDictionary<int, IReadOnlyList<int>> offeredByFormation)
    {
        var steps = new List<ClassStep>();
        var skipped = 0;
        foreach (var pair in preset.FormationClasses)
        {
            if (pair.Value <= 0) continue;
            var resolved = offeredByFormation.TryGetValue(pair.Key, out var offered) ? ResolveClass(pair.Value, offered) : null;
            if (resolved.HasValue) steps.Add(new ClassStep(pair.Key, resolved.Value));
            else skipped++;
        }
        return new ClassPlan(steps, skipped);
    }

    /// <summary>
    /// Plans the heroes of the formations in <paramref name="readyFormations"/> only: a hero is skipped when it is not in
    /// this battle or when its formation is not ready (see <see cref="PlanLoad"/>).
    /// </summary>
    public static HeroPlan PlanHeroes(HoNFormationPreset preset, ISet<string> heroesInBattle, ISet<int> readyFormations)
    {
        var captains = new List<HeroStep>();
        var troops = new List<HeroStep>();
        var skipped = 0;

        foreach (var heroId in preset.CaptainHeroIds)
        {
            if (!preset.HeroFormationAssignments.TryGetValue(heroId, out var index)
                || !heroesInBattle.Contains(heroId) || !readyFormations.Contains(index)) skipped++;
            else captains.Add(new HeroStep(heroId, index));
        }
        foreach (var pair in preset.HeroFormationAssignments)
        {
            if (preset.IsCaptain(pair.Key)) continue;
            if (!heroesInBattle.Contains(pair.Key) || !readyFormations.Contains(pair.Value)) skipped++;
            else troops.Add(new HeroStep(pair.Key, pair.Value));
        }
        return new HeroPlan(captains, troops, skipped);
    }

    /// <summary>
    /// The whole Load plan: the class steps, run through the pass loop with <paramref name="tryApplyClass"/> (the boundary
    /// changes the class, or reports it already matches), then the heroes of the formations whose class step took effect.
    /// A formation whose class step was blocked, or could not be mapped to a class this battle offers, takes no heroes: they
    /// would land in a formation of the wrong type.
    /// </summary>
    public static LoadPlan PlanLoad(HoNFormationPreset preset, IReadOnlyDictionary<int, IReadOnlyList<int>> offeredByFormation,
        Func<ClassStep, bool> tryApplyClass, ISet<string> heroesInBattle)
    {
        var classPlan = PlanClasses(preset, offeredByFormation);
        var passes = RunClassPasses(classPlan.Steps, tryApplyClass);
        var ready = new HashSet<int>(passes.Applied.Select(step => step.FormationIndex));
        var heroes = PlanHeroes(preset, heroesInBattle, ready);
        return new LoadPlan(passes, heroes, classPlan.Skipped + passes.Stuck.Count + heroes.Skipped);
    }

    /// <summary>
    /// Repeats passes over the pending class changes while the last pass applied at least one. A change can become possible
    /// only after another (the UI refuses a class change while the formation is the only one set to a class this battle has
    /// troops of), so a stuck step gets another chance after each pass that moved something. Every pass that does not stop
    /// removes a step, so the loop ends.
    /// </summary>
    public static PassResult RunClassPasses(IReadOnlyList<ClassStep> steps, Func<ClassStep, bool> tryApply)
    {
        var pending = steps.ToList();
        var applied = new List<ClassStep>();
        int appliedThisPass;
        do
        {
            appliedThisPass = 0;
            foreach (var step in pending.ToList())
            {
                if (!tryApply(step)) continue;
                pending.Remove(step);
                applied.Add(step);
                appliedThisPass++;
            }
        }
        while (pending.Count > 0 && appliedThisPass > 0);
        return new PassResult(applied, pending);
    }
}
