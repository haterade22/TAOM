using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TAOM.Composition;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Composition;

/// <summary>
/// Generic tests over <see cref="FeatureModules.All"/>: they replace per-feature text asserts on
/// SubModule.cs and IoC.cs as features migrate. The two Kernel tests pin the runner calls in the two
/// single-owner files, which are kernel wiring and never migrate.
/// </summary>
[TestClass]
public class FeatureModulesTests
{
    [TestMethod]
    public void EveryModule_HasAUniqueNonEmptyId()
    {
        var ids = FeatureModules.All.Select(m => m.Id).ToList();

        Assert.IsTrue(ids.All(id => !string.IsNullOrWhiteSpace(id)), "A feature module has an empty Id.");
        var duplicates = ids.GroupBy(id => id, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.AreEqual(0, duplicates.Count, "Duplicate module ids: " + string.Join(", ", duplicates));
    }

    [TestMethod]
    public void ParkedModules_CarryAReason_AndEnabledModulesDoNot()
    {
        foreach (var module in FeatureModules.All)
        {
            if (module.State == FeatureState.Parked)
                Assert.IsFalse(string.IsNullOrWhiteSpace(module.ParkedReason), $"{module.Id} is parked with no reason; name the issue.");
            else
                Assert.IsNull(module.ParkedReason, $"{module.Id} is enabled but carries a parked reason.");
        }
    }

    [TestMethod]
    public void EveryDeclaredCampaignBehavior_IsDeclaredOnce_AndSubModuleNoLongerAddsIt()
    {
        var declared = FeatureModules.All
            .SelectMany(m => m.CampaignBehaviors.Select(d => (Module: m.Id, Type: d.BehaviorType))).ToList();

        AssertDeclaredOnce(declared.Select(d => d.Type.FullName!), "campaign behavior");
        AssertNotHandWiredToo(declared, "added");
    }

    [TestMethod]
    public void EveryDeclaredMissionBehavior_IsDeclaredOnce_AndSubModuleNoLongerAddsIt()
    {
        var declared = FeatureModules.All
            .SelectMany(m => m.MissionBehaviors.Select(d => (Module: m.Id, Type: d.BehaviorType))).ToList();

        AssertDeclaredOnce(declared.Select(d => d.Type.FullName!), "mission behavior");
        AssertNotHandWiredToo(declared, "added");
    }

    [TestMethod]
    public void EveryDeclaredGameModel_SlotIsDeclaredOncePerTarget_AndSubModuleNoLongerAddsIt()
    {
        var declared = FeatureModules.All.SelectMany(m => m.GameModels.Select(d => (Module: m.Id, Decl: d))).ToList();

        // One engine model per slot: a second AddModel for the same slot silently shadows the first.
        AssertDeclaredOnce(declared.Select(d => d.Decl.Target + ":" + d.Decl.SlotType.FullName), "model slot");
        AssertNotHandWiredToo(declared.Select(d => (d.Module, Type: d.Decl.ModelType)).ToList(), "registered");
    }

    [TestMethod]
    public void EveryDeclaredPatchCategory_IsDeclaredOnce_AndSubModuleNoLongerAppliesIt()
    {
        var declared = FeatureModules.All.SelectMany(m => m.PatchCategories.Select(d => (Module: m.Id, d.Category))).ToList();
        var subModule = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        AssertDeclaredOnce(declared.Select(d => d.Category), "patch category");
        foreach (var (module, category) in declared)
        {
            Assert.IsFalse(subModule.Contains("\"" + category + "\""),
                $"{category} is declared by the {module} module AND still applied in SubModule.cs: Harmony would "
                + "apply it twice (duplicated prefixes and postfixes). Delete the SubModule line.");
        }
    }

    [TestMethod]
    public void ModulesWhoseBehaviorsPersistData_DeclareOwnsSaveData()
    {
        foreach (var module in FeatureModules.All)
        {
            foreach (var decl in module.CampaignBehaviors)
            {
                var syncData = decl.BehaviorType.GetMethod("SyncData",
                    BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(IDataStore) }, null);
                var il = syncData?.GetMethodBody()?.GetILAsByteArray();

                // An empty override compiles to "nop; ret" (2 bytes, Debug) or "ret" (1 byte, Release).
                if (il != null && il.Length > 2)
                    Assert.IsTrue(module.OwnsSaveData,
                        $"{decl.BehaviorType.Name} persists data in SyncData, so the {module.Id} module must set OwnsSaveData "
                        + "(it then fails closed instead of running a campaign without its persistence).");
            }
        }
    }

    [TestMethod]
    public void Kernel_IoCConfigure_RunsTheModuleStepsAfterEveryHandWiredRegistration()
    {
        var code = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true);

        AssertOnceBetween(code, "RegisterUncapturableHeroesFeature(container);",
            "modules.RegisterServices(container);", "_container = container;");
        AssertOnceBetween(code, "CareerSystemIoC.InitializeCalculators(",
            "modules.InitializeStatics(container);", "private static void RegisterCoreServices(");
    }

    [TestMethod]
    public void Kernel_SubModule_CallsEachRunnerHookOnce_AtTheEndOfItsFeatureBlock()
    {
        var code = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        // OnSubModuleLoad reports nothing (plan 009: no receiver yet), so its runner call is pinned
        // between its last category and the next method, not before a report call.
        AssertOnceBetween(code, "TryPatchCategory(\"Patch42_CastleRecruitment\");",
            "FeatureModuleHooks.RunPhase(ApplyPhase.ProcessLoad, TryPatchCategory);",
            "protected override void OnBeforeInitialModuleScreenSetAsRoot()");
        // Before 009's startup inquiry, so a module category that fails at MainMenu is in it.
        AssertOnceBetween(code, "TryPatchCategory(\"Patch55_BasicTableauRaceGuard\");",
            "FeatureModuleHooks.RunPhase(ApplyPhase.MainMenu, TryPatchCategory);",
            "ReportPatchFailures(\"startup\", persistent: true);");
        AssertOnceBetween(code, "RegisterCampaignLifeBehaviors(campaignStarter);",
            "FeatureModuleHooks.AddGameStartContent(gameStarterObject);", "public override void OnGameLoaded(");
        AssertOnceBetween(code, "TryPatchCategory(\"Patch69_TournamentEndGuard\");",
            "FeatureModuleHooks.RunPhase(ApplyPhase.GameInit, TryPatchCategory);", "ReportPatchFailures(\"game initialization\");");
        AssertOnceBetween(code, "TryPatchCategory(\"Patch_MissionTime_SetMovementOrder\");",
            "FeatureModuleHooks.RunPhase(ApplyPhase.FirstMission, TryPatchCategory);", "ReportPatchFailures(\"mission start\");");
        AssertOnceBetween(code, "new AgentColorStoreCleanupBehavior(colorStore)",
            "FeatureModuleHooks.AddMissionBehaviors(mission, AddTaomBehavior);", "new Features.MissionDiagnostic.Hooks.MissionDiagnosticBehavior(");
    }

    // Nothing receives a chat line before the initial screen, and the initial screen clears the chat
    // log after the splash video (docs/reviews/lessons/localization-ui.md). Startup faults are held
    // for the main-menu inquiry; in-game phases have a chat log to take a red line.
    [TestMethod]
    public void FaultNotice_HoldsProcessLoad_InquiresAtMainMenu_AndUsesAChatLineInGame()
    {
        Assert.AreEqual(FaultNotice.Hold, FeatureModuleHooks.NoticeFor(ApplyPhase.ProcessLoad));
        Assert.AreEqual(FaultNotice.Inquiry, FeatureModuleHooks.NoticeFor(ApplyPhase.MainMenu));
        Assert.AreEqual(FaultNotice.ChatLine, FeatureModuleHooks.NoticeFor(ApplyPhase.GameInit));
        Assert.AreEqual(FaultNotice.ChatLine, FeatureModuleHooks.NoticeFor(ApplyPhase.FirstMission));
    }

    private static void AssertDeclaredOnce(IEnumerable<string> keys, string what)
    {
        var duplicates = keys.GroupBy(k => k, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.AreEqual(0, duplicates.Count, $"Declared more than once ({what}): " + string.Join(", ", duplicates));
    }

    // Transitional, until the last feature migrates: a type a module declares must not also be built
    // by hand in SubModule.cs ("new X(" or "Resolve<...X>()"), or the engine gets it twice.
    private static void AssertNotHandWiredToo(IReadOnlyList<(string Module, Type Type)> declared, string verb)
    {
        var subModule = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        foreach (var (module, type) in declared)
        {
            var name = Regex.Escape(type.Name);
            var handWired = Regex.IsMatch(subModule,
                @"new\s+(?:\w+\.)*" + name + @"\s*\(|Resolve<(?:\w+\.)*" + name + @">\s*\(");
            Assert.IsFalse(handWired,
                $"{type.Name} is declared by the {module} module AND still {verb} by hand in SubModule.cs. Delete the SubModule line.");
        }
    }

    private static void AssertOnceBetween(string code, string after, string call, string before)
    {
        var at = code.IndexOf(call, StringComparison.Ordinal);
        Assert.AreNotEqual(-1, at, $"Missing runner call: {call}");
        Assert.AreEqual(-1, code.IndexOf(call, at + call.Length, StringComparison.Ordinal), $"Runner call appears twice: {call}");

        var afterAt = code.IndexOf(after, StringComparison.Ordinal);
        var beforeAt = code.IndexOf(before, StringComparison.Ordinal);
        Assert.IsTrue(afterAt >= 0 && beforeAt >= 0, $"An anchor around {call} is missing: '{after}' or '{before}'.");
        Assert.IsTrue(afterAt < at && at < beforeAt, $"{call} must sit after '{after}' and before '{before}'.");
    }
}
