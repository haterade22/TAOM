using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.CompanionTactics;

/// <summary>
/// Phase 9b #182 — source-content assertion tests verifying CompanionTactics and SmartCavalryAI
/// share the deferred Patch_MissionTime_SetMovementOrder Harmony category, that both target
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
    public void Patch31_SmartCavalryAI_DeclaresSharedMissionTimeCategory()
    {
        var repoRoot = FindRepoRoot();
        var path = Path.Combine(repoRoot, "Main", "Features", "SmartCavalryAI", "Hooks", "Patch31_FormationSetMovementOrder.cs");
        Assert.IsTrue(File.Exists(path), $"Patch31 file missing: {path}");
        var src = File.ReadAllText(path);
        StringAssert.Contains(src, "[HarmonyPatchCategory(\"Patch_MissionTime_SetMovementOrder\")]",
            "Patch31 must declare the shared category so OnMissionBehaviorInitialize applies it");
        StringAssert.Contains(src, "SetMovementOrder",
            "Patch31 must target Formation.SetMovementOrder");
    }

    [TestMethod]
    public void Patch35_CompanionTactics_DeclaresSharedMissionTimeCategory()
    {
        var repoRoot = FindRepoRoot();
        var path = Path.Combine(repoRoot, "Main", "Features", "CompanionTactics", "BattleActionBar", "Hooks", "Patch35_Formation_SetMovementOrder.cs");
        Assert.IsTrue(File.Exists(path), $"Patch35 file missing: {path}");
        var src = File.ReadAllText(path);
        StringAssert.Contains(src, "[HarmonyPatchCategory(\"Patch_MissionTime_SetMovementOrder\")]",
            "Patch35 must declare the shared category so OnMissionBehaviorInitialize applies it");
        StringAssert.Contains(src, "SetMovementOrder",
            "Patch35 must target Formation.SetMovementOrder");
    }

    [TestMethod]
    public void SubModule_AppliesSharedCategoryInMissionInit_NotOnSubModuleLoad()
    {
        // Phase 9b — MovementOrder.cctor reads Mission.Current.CurrentTime; null during
        // OnSubModuleLoad → crashes JIT prep with NRE. The shared category MUST be applied via
        // OnMissionBehaviorInitialize with a one-shot guard. Verify the comment + apply site.
        var repoRoot = FindRepoRoot();
        var path = Path.Combine(repoRoot, "Main", "SubModule.cs");
        Assert.IsTrue(File.Exists(path), $"SubModule.cs missing: {path}");
        var src = File.ReadAllText(path);
        StringAssert.Contains(src, "Patch_MissionTime_SetMovementOrder",
            "SubModule.cs must reference the shared category");
        StringAssert.Contains(src, "_missionTimePatchesApplied",
            "SubModule.cs must use a one-shot guard for the deferred apply");
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
}
