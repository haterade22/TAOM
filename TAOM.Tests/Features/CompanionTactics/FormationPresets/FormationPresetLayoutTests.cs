using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CompanionTactics.FormationPresets;
using TAOM.Features.CompanionTactics.FormationPresets.Models;

namespace TAOM.Tests.Features.CompanionTactics.FormationPresets;

/// <summary>
/// The pure mapper between the Order of Battle layout and a <see cref="HoNFormationPreset"/>. No game
/// types: the boundary (<c>OOBPresetApplier</c>) feeds it plain snapshots and delegates.
/// DeploymentFormationClass values: 0 Unset, 1 Infantry, 2 Ranged, 3 Cavalry, 4 HorseArcher,
/// 5 InfantryAndRanged, 6 CavalryAndHorseArcher.
/// </summary>
[TestClass]
public class FormationPresetLayoutTests
{
    private static readonly int[] AllClasses = { 0, 1, 2, 3, 4, 5, 6 };
    private static readonly int[] SiegeClasses = { 0, 1, 2, 5 };

    private static PresetFormationSnapshot Snap(int index, int cls, string? captain = null, string[]? troops = null) =>
        new(index, cls, captain!, troops ?? new string[0]);

    private static HoNFormationPreset Preset(Dictionary<int, int>? classes = null, string[]? captains = null,
        Dictionary<string, int>? assignments = null)
    {
        var preset = new HoNFormationPreset("p");
        if (classes != null) preset.FormationClasses = classes;
        if (captains != null) preset.CaptainHeroIds = captains.ToList();
        if (assignments != null) preset.HeroFormationAssignments = assignments;
        return preset;
    }

    // ---- Capture ----

    [TestMethod]
    public void Capture_AnyLayout_NamesThePreset()
    {
        var preset = FormationPresetLayout.Capture("Siege plan", new[] { Snap(0, 1) });

        Assert.AreEqual("Siege plan", preset.Name);
    }

    [TestMethod]
    public void Capture_FormationWithAClass_StoresTheClass()
    {
        var preset = FormationPresetLayout.Capture("p", new[] { Snap(0, 1), Snap(1, 5) });

        Assert.AreEqual(1, preset.FormationClasses[0]);
        Assert.AreEqual(5, preset.FormationClasses[1]);
    }

    [TestMethod]
    public void Capture_UnsetClass_StoresMinusOne()
    {
        var preset = FormationPresetLayout.Capture("p", new[] { Snap(2, 0) });

        Assert.AreEqual(-1, preset.FormationClasses[2]);
    }

    [TestMethod]
    public void Capture_Captain_IsAssignedToItsFormationAndListedAsCaptain()
    {
        var preset = FormationPresetLayout.Capture("p", new[] { Snap(0, 1, captain: "lord_a") });

        Assert.AreEqual(0, preset.HeroFormationAssignments["lord_a"]);
        CollectionAssert.AreEqual(new[] { "lord_a" }, preset.CaptainHeroIds);
    }

    [TestMethod]
    public void Capture_HeroTroops_AreAssignedButNotListedAsCaptains()
    {
        var preset = FormationPresetLayout.Capture("p", new[] { Snap(4, 2, troops: new[] { "t1", "t2" }) });

        Assert.AreEqual(4, preset.HeroFormationAssignments["t1"]);
        Assert.AreEqual(4, preset.HeroFormationAssignments["t2"]);
        Assert.AreEqual(0, preset.CaptainHeroIds.Count);
    }

    [TestMethod]
    public void Capture_MainHeroLeadingAFormation_IsIncludedLikeAnyHero()
    {
        var preset = FormationPresetLayout.Capture("p", new[] { Snap(0, 1, captain: "main_hero") });

        Assert.AreEqual(0, preset.HeroFormationAssignments["main_hero"]);
        Assert.IsTrue(preset.IsCaptain("main_hero"));
    }

    [TestMethod]
    public void Capture_CaptainAndTroopInDifferentFormations_KeepsEachOwnIndex()
    {
        var preset = FormationPresetLayout.Capture("p", new[]
        {
            Snap(0, 1, captain: "cap"),
            Snap(5, 2, troops: new[] { "troop" }),
        });

        Assert.AreEqual(0, preset.HeroFormationAssignments["cap"]);
        Assert.AreEqual(5, preset.HeroFormationAssignments["troop"]);
        CollectionAssert.AreEqual(new[] { "cap" }, preset.CaptainHeroIds);
    }

    [TestMethod]
    public void Capture_SameHeroTwice_ListsTheCaptainOnce()
    {
        var preset = FormationPresetLayout.Capture("p", new[]
        {
            Snap(0, 1, captain: "x"),
            Snap(1, 1, captain: "x"),
        });

        Assert.AreEqual(1, preset.CaptainHeroIds.Count);
    }

    [TestMethod]
    public void Capture_NoFormations_GivesAnEmptyPreset()
    {
        var preset = FormationPresetLayout.Capture("p", new PresetFormationSnapshot[0]);

        Assert.AreEqual(0, preset.FormationClasses.Count);
        Assert.AreEqual(0, preset.HeroFormationAssignments.Count);
    }

    // ---- Counts ----

    [TestMethod]
    public void CountOf_MixedLayout_CountsClassesCaptainsAndTroopsSeparately()
    {
        var preset = FormationPresetLayout.Capture("p", new[]
        {
            Snap(0, 1, captain: "cap", troops: new[] { "t1" }),
            Snap(1, 2, troops: new[] { "t2", "t3" }),
            Snap(2, 0),
        });

        var counts = FormationPresetLayout.CountOf(preset);

        Assert.AreEqual(2, counts.Classes);
        Assert.AreEqual(1, counts.Captains);
        Assert.AreEqual(3, counts.Troops);
    }

    [TestMethod]
    public void CountOf_PresetSavedByTheOldNameOnlyBuild_IsAllZero()
    {
        var counts = FormationPresetLayout.CountOf(new HoNFormationPreset("old"));

        Assert.AreEqual(0, counts.Classes);
        Assert.AreEqual(0, counts.Captains);
        Assert.AreEqual(0, counts.Troops);
    }

    // ---- ResolveClass ----

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    public void ResolveClass_SavedClassIsOffered_ReturnsIt(int saved)
    {
        Assert.AreEqual(saved, FormationPresetLayout.ResolveClass(saved, AllClasses));
    }

    [TestMethod]
    [DataRow(4, 2)]
    [DataRow(3, 1)]
    [DataRow(6, 5)]
    public void ResolveClass_SiegeOffersNoMountedClass_UsesTheVanillaMapping(int saved, int expected)
    {
        Assert.AreEqual(expected, FormationPresetLayout.ResolveClass(saved, SiegeClasses));
    }

    [TestMethod]
    public void ResolveClass_MappedClassIsNotOfferedEither_ReturnsNone()
    {
        Assert.IsNull(FormationPresetLayout.ResolveClass(4, new[] { 0, 1 }));
    }

    [TestMethod]
    public void ResolveClass_ClassUnknownToTheMapping_ReturnsNone()
    {
        Assert.IsNull(FormationPresetLayout.ResolveClass(9, SiegeClasses));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void ResolveClass_UnsetOrNegative_IsNeverApplied(int saved)
    {
        Assert.IsNull(FormationPresetLayout.ResolveClass(saved, AllClasses));
    }

    // ---- PlanClasses ----

    private static Dictionary<int, IReadOnlyList<int>> Offered(params int[] indices) =>
        indices.ToDictionary(i => i, _ => (IReadOnlyList<int>)AllClasses);

    [TestMethod]
    public void PlanClasses_SetClasses_BecomeSteps()
    {
        var preset = Preset(classes: new Dictionary<int, int> { [0] = 1, [1] = 2 });

        var plan = FormationPresetLayout.PlanClasses(preset, Offered(0, 1));

        CollectionAssert.AreEqual(new[] { new ClassStep(0, 1), new ClassStep(1, 2) }, plan.Steps.ToArray());
        Assert.AreEqual(0, plan.Skipped);
    }

    [TestMethod]
    public void PlanClasses_UnsetEntries_AreIgnoredAndNotCounted()
    {
        var preset = Preset(classes: new Dictionary<int, int> { [0] = -1, [1] = 0 });

        var plan = FormationPresetLayout.PlanClasses(preset, Offered(0, 1));

        Assert.AreEqual(0, plan.Steps.Count);
        Assert.AreEqual(0, plan.Skipped);
    }

    [TestMethod]
    public void PlanClasses_FormationNotInThisBattle_IsSkippedAndCounted()
    {
        var preset = Preset(classes: new Dictionary<int, int> { [7] = 1 });

        var plan = FormationPresetLayout.PlanClasses(preset, Offered(0, 1));

        Assert.AreEqual(0, plan.Steps.Count);
        Assert.AreEqual(1, plan.Skipped);
    }

    [TestMethod]
    public void PlanClasses_UnmappableClass_IsSkippedAndCounted()
    {
        var preset = Preset(classes: new Dictionary<int, int> { [0] = 4 });
        var offered = new Dictionary<int, IReadOnlyList<int>> { [0] = new[] { 0, 1 } };

        var plan = FormationPresetLayout.PlanClasses(preset, offered);

        Assert.AreEqual(0, plan.Steps.Count);
        Assert.AreEqual(1, plan.Skipped);
    }

    [TestMethod]
    public void PlanClasses_SiegeFormation_StepCarriesTheMappedClass()
    {
        var preset = Preset(classes: new Dictionary<int, int> { [0] = 6 });
        var offered = new Dictionary<int, IReadOnlyList<int>> { [0] = SiegeClasses };

        var plan = FormationPresetLayout.PlanClasses(preset, offered);

        CollectionAssert.AreEqual(new[] { new ClassStep(0, 5) }, plan.Steps.ToArray());
    }

    // ---- PlanHeroes (the third argument is the formations whose class is in place) ----

    private static ISet<string> Heroes(params string[] ids) => new HashSet<string>(ids);
    private static ISet<int> Formations(params int[] ids) => new HashSet<int>(ids);

    [TestMethod]
    public void PlanHeroes_CaptainAndTroop_SplitsCaptainsFromTroops()
    {
        var preset = Preset(captains: new[] { "cap" },
            assignments: new Dictionary<string, int> { ["cap"] = 0, ["troop"] = 3 });

        var plan = FormationPresetLayout.PlanHeroes(preset, Heroes("cap", "troop"), Formations(0, 3));

        CollectionAssert.AreEqual(new[] { new HeroStep("cap", 0) }, plan.Captains.ToArray());
        CollectionAssert.AreEqual(new[] { new HeroStep("troop", 3) }, plan.Troops.ToArray());
        Assert.AreEqual(0, plan.Skipped);
    }

    [TestMethod]
    public void PlanHeroes_HeroNotInThisBattle_IsSkippedAndCounted()
    {
        var preset = Preset(captains: new[] { "gone" },
            assignments: new Dictionary<string, int> { ["gone"] = 0, ["away"] = 1, ["here"] = 1 });

        var plan = FormationPresetLayout.PlanHeroes(preset, Heroes("here"), Formations(0, 1));

        Assert.AreEqual(0, plan.Captains.Count);
        CollectionAssert.AreEqual(new[] { new HeroStep("here", 1) }, plan.Troops.ToArray());
        Assert.AreEqual(2, plan.Skipped);
    }

    [TestMethod]
    public void PlanHeroes_FormationNotInThisBattle_IsSkippedAndCounted()
    {
        var preset = Preset(captains: new[] { "cap" },
            assignments: new Dictionary<string, int> { ["cap"] = 6, ["troop"] = 7 });

        var plan = FormationPresetLayout.PlanHeroes(preset, Heroes("cap", "troop"), Formations(0, 1));

        Assert.AreEqual(0, plan.Captains.Count);
        Assert.AreEqual(0, plan.Troops.Count);
        Assert.AreEqual(2, plan.Skipped);
    }

    [TestMethod]
    public void PlanHeroes_CaptainWithNoAssignment_IsSkippedAndCounted()
    {
        var preset = Preset(captains: new[] { "orphan" });

        var plan = FormationPresetLayout.PlanHeroes(preset, Heroes("orphan"), Formations(0));

        Assert.AreEqual(0, plan.Captains.Count);
        Assert.AreEqual(1, plan.Skipped);
    }

    [TestMethod]
    public void PlanHeroes_EmptyPreset_PlansNothing()
    {
        var plan = FormationPresetLayout.PlanHeroes(new HoNFormationPreset("old"), Heroes("a"), Formations(0));

        Assert.AreEqual(0, plan.Captains.Count);
        Assert.AreEqual(0, plan.Troops.Count);
        Assert.AreEqual(0, plan.Skipped);
    }

    // ---- RunClassPasses ----

    private static List<ClassStep> Steps(int count) =>
        Enumerable.Range(0, count).Select(i => new ClassStep(i, 1)).ToList();

    private static int[] Indices(IEnumerable<ClassStep> steps) => steps.Select(step => step.FormationIndex).ToArray();

    [TestMethod]
    public void RunClassPasses_AllSucceedInFirstPass_AppliesAllInOnePass()
    {
        var calls = 0;

        var result = FormationPresetLayout.RunClassPasses(Steps(3), _ => { calls++; return true; });

        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, Indices(result.Applied));
        Assert.AreEqual(0, result.Stuck.Count);
        Assert.AreEqual(3, calls);
    }

    [TestMethod]
    public void RunClassPasses_OneNeedsAnotherFirst_IsAppliedInALaterPass()
    {
        var applied = new HashSet<int>();
        // Formation 0 only becomes adjustable once formation 1 has been changed.
        bool TryApply(ClassStep step)
        {
            if (step.FormationIndex == 0 && !applied.Contains(1)) return false;
            applied.Add(step.FormationIndex);
            return true;
        }

        var result = FormationPresetLayout.RunClassPasses(Steps(2), TryApply);

        CollectionAssert.AreEquivalent(new[] { 0, 1 }, Indices(result.Applied));
        Assert.AreEqual(0, result.Stuck.Count);
    }

    [TestMethod]
    public void RunClassPasses_ChainResolvingBackwards_NeedsAsManyPassesAsStepsAndFinishes()
    {
        var applied = new HashSet<int>();
        // Step i needs step i + 1 first, so each pass applies exactly one.
        bool TryApply(ClassStep step)
        {
            if (step.FormationIndex < 3 && !applied.Contains(step.FormationIndex + 1)) return false;
            applied.Add(step.FormationIndex);
            return true;
        }

        var result = FormationPresetLayout.RunClassPasses(Steps(4), TryApply);

        Assert.AreEqual(4, result.Applied.Count);
        Assert.AreEqual(0, result.Stuck.Count);
    }

    [TestMethod]
    public void RunClassPasses_NoneSucceed_StopsAfterOnePassAndReturnsAllStuck()
    {
        var calls = 0;

        var result = FormationPresetLayout.RunClassPasses(Steps(3), _ => { calls++; return false; });

        Assert.AreEqual(0, result.Applied.Count);
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, Indices(result.Stuck));
        Assert.AreEqual(3, calls, "A pass that applies nothing ends the loop; it never spins.");
    }

    [TestMethod]
    public void RunClassPasses_SomeNeverSucceed_ReturnsTheStuckSteps()
    {
        var result = FormationPresetLayout.RunClassPasses(Steps(3), step => step.FormationIndex != 1);

        CollectionAssert.AreEqual(new[] { 0, 2 }, Indices(result.Applied));
        CollectionAssert.AreEqual(new[] { 1 }, Indices(result.Stuck));
    }

    [TestMethod]
    public void RunClassPasses_NoSteps_DoesNothing()
    {
        var calls = 0;

        var result = FormationPresetLayout.RunClassPasses(new List<ClassStep>(), _ => { calls++; return true; });

        Assert.AreEqual(0, result.Applied.Count);
        Assert.AreEqual(0, result.Stuck.Count);
        Assert.AreEqual(0, calls);
    }

    // ---- PlanLoad ----

    private static Dictionary<int, IReadOnlyList<int>> OfferedAll(params int[] indices) =>
        indices.ToDictionary(i => i, _ => (IReadOnlyList<int>)AllClasses);

    private static HoNFormationPreset CaptainAndTroopInFormationZero() => Preset(
        classes: new Dictionary<int, int> { [0] = 1 },
        captains: new[] { "cap" },
        assignments: new Dictionary<string, int> { ["cap"] = 0, ["troop"] = 0 });

    [TestMethod]
    public void PlanLoad_ClassStepApplied_PlansItsHeroes()
    {
        var plan = FormationPresetLayout.PlanLoad(CaptainAndTroopInFormationZero(), OfferedAll(0), _ => true, Heroes("cap", "troop"));

        CollectionAssert.AreEqual(new[] { new HeroStep("cap", 0) }, plan.Heroes.Captains.ToArray());
        CollectionAssert.AreEqual(new[] { new HeroStep("troop", 0) }, plan.Heroes.Troops.ToArray());
        Assert.AreEqual(1, plan.Classes.Applied.Count);
        Assert.AreEqual(0, plan.Skipped);
    }

    [TestMethod]
    public void PlanLoad_FormationAlreadyHasTheClass_PlansItsHeroes()
    {
        // The boundary reports an already matching formation as a step that succeeded with no change.
        var checkedSteps = new List<ClassStep>();
        bool AlreadyMatches(ClassStep step) { checkedSteps.Add(step); return true; }

        var plan = FormationPresetLayout.PlanLoad(CaptainAndTroopInFormationZero(), OfferedAll(0), AlreadyMatches, Heroes("cap", "troop"));

        CollectionAssert.AreEqual(new[] { new ClassStep(0, 1) }, checkedSteps);
        Assert.AreEqual(1, plan.Heroes.Captains.Count);
        Assert.AreEqual(1, plan.Heroes.Troops.Count);
    }

    [TestMethod]
    public void PlanLoad_ClassStepBlocked_SkipsItsHeroesAndCountsEverything()
    {
        var plan = FormationPresetLayout.PlanLoad(CaptainAndTroopInFormationZero(), OfferedAll(0), _ => false, Heroes("cap", "troop"));

        Assert.AreEqual(0, plan.Heroes.Captains.Count);
        Assert.AreEqual(0, plan.Heroes.Troops.Count);
        CollectionAssert.AreEqual(new[] { new ClassStep(0, 1) }, plan.Classes.Stuck.ToArray());
        Assert.AreEqual(3, plan.Skipped, "the stuck class change, the captain and the hero troop");
    }

    [TestMethod]
    public void PlanLoad_ClassUnmappableInThisBattle_SkipsItsHeroesAndCountsEverything()
    {
        // A siege formation: neither the saved class (4) nor its mapping (2) is offered.
        var preset = Preset(
            classes: new Dictionary<int, int> { [0] = 4 },
            captains: new[] { "cap" },
            assignments: new Dictionary<string, int> { ["cap"] = 0, ["troop"] = 0 });
        var offered = new Dictionary<int, IReadOnlyList<int>> { [0] = new[] { 0, 1 } };
        var tried = 0;

        var plan = FormationPresetLayout.PlanLoad(preset, offered, _ => { tried++; return true; }, Heroes("cap", "troop"));

        Assert.AreEqual(0, tried, "an unmappable class is never tried");
        Assert.AreEqual(0, plan.Heroes.Captains.Count);
        Assert.AreEqual(0, plan.Heroes.Troops.Count);
        Assert.AreEqual(3, plan.Skipped, "the unmappable class, the captain and the hero troop");
    }

    [TestMethod]
    public void PlanLoad_OneFormationReadyOneBlocked_PlansOnlyTheReadyOnesHeroes()
    {
        var preset = Preset(
            classes: new Dictionary<int, int> { [0] = 1, [1] = 2 },
            captains: new[] { "capA", "capB" },
            assignments: new Dictionary<string, int> { ["capA"] = 0, ["capB"] = 1 });

        var plan = FormationPresetLayout.PlanLoad(preset, OfferedAll(0, 1), step => step.FormationIndex == 0, Heroes("capA", "capB"));

        CollectionAssert.AreEqual(new[] { new HeroStep("capA", 0) }, plan.Heroes.Captains.ToArray());
        Assert.AreEqual(2, plan.Skipped, "the stuck class change of formation 1 and its captain");
    }

    [TestMethod]
    public void PlanLoad_HeroNotInBattle_CountsItAsSkipped()
    {
        var plan = FormationPresetLayout.PlanLoad(CaptainAndTroopInFormationZero(), OfferedAll(0), _ => true, Heroes("cap"));

        Assert.AreEqual(1, plan.Heroes.Captains.Count);
        Assert.AreEqual(0, plan.Heroes.Troops.Count);
        Assert.AreEqual(1, plan.Skipped);
    }
}
