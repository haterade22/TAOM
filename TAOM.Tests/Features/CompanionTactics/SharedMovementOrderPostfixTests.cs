using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.CompanionTactics;

/// <summary>
/// Phase 9b #182 — source-content assertion tests verifying CompanionTactics and SmartCavalryAI
/// each declare their own deferred Patch_MissionTime_* Harmony category (split 2026-11-13 so one
/// failing class turns off only its own feature), that both target
/// Formation.SetMovementOrder, and that they have non-overlapping intents (charge state vs
/// stance display). These are source-content tests, not runtime tests — Formation is sealed and
/// can't be constructed in unit tests; the integration is validated in-game via Mission boot.
/// </summary>
[TestClass]
public class SharedMovementOrderPostfixTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new System.IO.FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }

    [TestMethod]
    public void Patch31_SmartCavalryAI_DeclaresItsOwnMissionTimeCategory()
    {
        var repoRoot = FindRepoRoot();
        var path = Path.Combine(repoRoot, "Main", "Features", "SmartCavalryAI", "Hooks", "Patch31_FormationSetMovementOrder.cs");
        Assert.IsTrue(File.Exists(path), $"Patch31 file missing: {path}");
        var src = File.ReadAllText(path);
        StringAssert.Contains(src, "[HarmonyPatchCategory(\"Patch_MissionTime_SmartCavalryAI\")]",
            "Patch31 must declare its own deferred category so OnMissionBehaviorInitialize applies it");
        StringAssert.Contains(src, "SetMovementOrder",
            "Patch31 must target Formation.SetMovementOrder");
    }

    [TestMethod]
    public void Patch35_CompanionTactics_DeclaresItsOwnMissionTimeCategory()
    {
        var repoRoot = FindRepoRoot();
        var path = Path.Combine(repoRoot, "Main", "Features", "CompanionTactics", "BattleActionBar", "Hooks", "Patch35_Formation_SetMovementOrder.cs");
        Assert.IsTrue(File.Exists(path), $"Patch35 file missing: {path}");
        var src = File.ReadAllText(path);
        StringAssert.Contains(src, "[HarmonyPatchCategory(\"Patch_MissionTime_CompanionTactics\")]",
            "Patch35 must declare its own deferred category so OnMissionBehaviorInitialize applies it");
        StringAssert.Contains(src, "SetMovementOrder",
            "Patch35 must target Formation.SetMovementOrder");
    }

    [TestMethod]
    public void SubModule_AppliesSharedCategoryInMissionInit_NotOnSubModuleLoad()
    {
        // Phase 9b — MovementOrder.cctor reads Mission.Current.CurrentTime; null during
        // OnSubModuleLoad → crashes JIT prep with NRE. Both per-feature categories MUST be applied via
        // OnMissionBehaviorInitialize with a one-shot guard. Verify the comment + apply site.
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        StringAssert.Contains(src, "Patch_MissionTime_SmartCavalryAI",
            "SubModule.cs must apply the SmartCavalryAI category");
        StringAssert.Contains(src, "Patch_MissionTime_CompanionTactics",
            "SubModule.cs must apply the CompanionTactics category");
        StringAssert.Contains(src, "_missionTimePatchesApplied",
            "SubModule.cs must use a one-shot guard for the deferred apply");
    }

    // One failing class used to stop the whole shared category, so a SmartCavalry break after a game update
    // also turned off Companion Tactics and Marshal's Word. Each feature now has its own category. SubModule
    // applies SmartCavalry's before Companion Tactics', the order Harmony used before (assembly order), so
    // the two postfixes on Formation.SetMovementOrder keep their order; Marshal's Word's is its module's.
    [TestMethod]
    public void SubModule_AppliesTheMissionTimeCategoriesSeparately_InTheOldOrder()
    {
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        Assert.IsFalse(src.Contains("\"Patch_MissionTime_SetMovementOrder\""), "the shared category is no longer applied");
        int cavalry = src.IndexOf("TryPatchCategory(\"Patch_MissionTime_SmartCavalryAI\");", System.StringComparison.Ordinal);
        int tactics = src.IndexOf("TryPatchCategory(\"Patch_MissionTime_CompanionTactics\");", System.StringComparison.Ordinal);
        Assert.IsTrue(cavalry >= 0 && tactics >= 0, "both categories are applied");
        Assert.IsTrue(cavalry < tactics, "SmartCavalry before Companion Tactics");
    }

    [TestMethod]
    public void NoPatchClass_DeclaresTheOldSharedCategory()
    {
        foreach (var file in RepoPaths.CSharpFiles())
        {
            var src = RepoPaths.Text(file);
            Assert.IsFalse(src.Contains("[HarmonyPatchCategory(\"Patch_MissionTime_SetMovementOrder\")]"),
                file + " still declares the shared Patch_MissionTime_SetMovementOrder category");
        }
    }

    [TestMethod]
    public void Patch31_DoesNotCancelStance_NonOverlappingIntent()
    {
        // Patch31 mutates CavalryChargeService state — must NOT call CancelStance/related.
        // Symmetric: Patch35 cancels stance — must NOT mutate cavalry charge state.
        var repoRoot = FindRepoRoot();
        var path = Path.Combine(repoRoot, "Main", "Features", "SmartCavalryAI", "Hooks", "Patch31_FormationSetMovementOrder.cs");
        var src = File.ReadAllText(path);
        Assert.IsFalse(src.Contains("CancelStance"),
            "Patch31 must not invoke CancelStance — that is Patch35's domain (non-overlapping intent)");
    }

    [TestMethod]
    public void Patch35_DoesNotTouchCavalryChargeService_NonOverlappingIntent()
    {
        var repoRoot = FindRepoRoot();
        var path = Path.Combine(repoRoot, "Main", "Features", "CompanionTactics", "BattleActionBar", "Hooks", "Patch35_Formation_SetMovementOrder.cs");
        var src = File.ReadAllText(path);
        Assert.IsFalse(src.Contains("CavalryChargeService") || src.Contains("ICavalryChargeService"),
            "Patch35 must not touch the cavalry charge service — that is Patch31's domain (non-overlapping intent)");
    }

    // Formation.SetMovementOrder runs for every team's formations, mostly on the async AI tick. The team
    // filter is two field reads; checking it first skips the settings read for every enemy order.
    [TestMethod]
    public void Patch35_FiltersThePlayerTeam_BeforeReadingTheSetting()
    {
        var src = RepoPaths.ReadSource(
            "Main/Features/CompanionTactics/BattleActionBar/Hooks/Patch35_Formation_SetMovementOrder.cs",
            stripComments: true);
        int team = src.IndexOf("Mission.Current?.PlayerTeam", System.StringComparison.Ordinal);
        int setting = src.IndexOf(".CancelStanceOnMove", System.StringComparison.Ordinal);
        Assert.IsTrue(team >= 0 && setting >= 0, "both checks are still present");
        Assert.IsTrue(team < setting, "the player-team filter runs before the CancelStanceOnMove read");
    }
}
