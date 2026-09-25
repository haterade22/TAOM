using System;
using System.Collections.Generic;
using System.IO;
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
    public void ParkedModules_NameTheirReason()
    {
        foreach (var module in FeatureModules.All)
        {
            if (module.ParkedReason != null)
                Assert.IsFalse(string.IsNullOrWhiteSpace(module.ParkedReason), $"{module.Id} is parked with a blank reason; name the issue.");
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

        // One engine model per slot among the modules: a second AddModel for the same slot silently
        // shadows the first. Not checked yet: a module slot that SubModule also fills by hand with a
        // different type (the first model migration adds that, with gamemodels.md rule 7).
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
                if (PersistsData(decl.BehaviorType))
                    Assert.IsTrue(module.OwnsSaveData,
                        $"{decl.BehaviorType.Name} persists data in SyncData, so the {module.Id} module must set OwnsSaveData "
                        + "(it then fails closed instead of running a campaign without its persistence).");
            }
        }
    }

    // Positive and negative control for the IL check above (review of plan 018, lens 4 F4): the pilot's
    // behavior alone cannot show that the check ever fires.
    [TestMethod]
    public void PersistsData_FlagsARealSyncData_AndPassesAnEmptyOne()
    {
        Assert.IsTrue(PersistsData(typeof(TAOM.Features.FieldCamp.Hooks.FieldCampCampaignBehavior)));
        Assert.IsFalse(PersistsData(typeof(TAOM.Features.WandererAllegiance.Hooks.WandererAllegianceDialogBehavior)));
    }

    // Review of plan 018 (lens 1): IoC.Resolver hands the whole container to Main, and a service
    // reaching it would be a service locator the "IoC.Resolve<" review grep cannot see.
    [TestMethod]
    public void IoCResolver_IsReadOnlyByTheFeatureModuleHooks()
    {
        var mainDir = RepoPaths.RepoPath("Main");
        var readers = Directory.GetFiles(mainDir, "*.cs", SearchOption.AllDirectories)
            .Select(f => f.Substring(mainDir.Length + 1).Replace('\\', '/'))
            .Where(rel => !rel.StartsWith("obj/", StringComparison.Ordinal) && !rel.StartsWith("bin/", StringComparison.Ordinal))
            .Where(rel => RepoPaths.StripComments(File.ReadAllText(Path.Combine(mainDir, rel))).Contains("IoC.Resolver"))
            .ToList();

        CollectionAssert.AreEquivalent(new[] { "Composition/FeatureModuleHooks.cs" }, readers,
            "Only the feature-module hooks may read IoC.Resolver; a service takes its dependencies by constructor.");
    }

    [TestMethod]
    public void Kernel_IoCConfigure_RunsTheModuleStepsAfterEveryHandWiredRegistration()
    {
        var code = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true);

        AssertOnceBetween(code, "RegisterUncapturableHeroesFeature(container);",
            "modules.RegisterServices(container);", "_container = container;");
        // The one line that hands the runner to FeatureModuleHooks (lens 4 F1): without it every hook
        // returns early and every module silently does nothing.
        AssertOnceBetween(code, "modules.RegisterServices(container);", "Modules = modules;", "_container = container;");
        AssertOnceBetween(code, "CareerSystemIoC.InitializeCalculators(",
            "modules.InitializeStatics(container);", "private static void RegisterCoreServices(");
    }

    [TestMethod]
    public void Kernel_SubModule_CallsEachRunnerHookOnce_AfterItsFeatureBlock()
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
            "ReportPatchFailures(new TextObject(\"{=taom_patch_apply_phase_startup}startup\"), persistent: true);");
        AssertOnceBetween(code, "RegisterCampaignLifeBehaviors(campaignStarter);",
            "FeatureModuleHooks.AddGameStartContent(gameStarterObject);", "public override void OnGameLoaded(");
        // Outside the CampaignGameStarter branch (lens 5 F4): inside it, a Custom Battle starter never
        // reaches the hook and every CustomBattle-target model is dropped silently.
        var lifeAt = code.IndexOf("RegisterCampaignLifeBehaviors(campaignStarter);", StringComparison.Ordinal);
        var hookAt = code.IndexOf("FeatureModuleHooks.AddGameStartContent(gameStarterObject);", StringComparison.Ordinal);
        StringAssert.Contains(code.Substring(lifeAt, hookAt - lifeAt), "}",
            "AddGameStartContent must follow the close of the CampaignGameStarter branch in OnGameStart.");
        AssertOnceBetween(code, "TryPatchCategory(\"Patch69_TournamentEndGuard\");",
            "FeatureModuleHooks.RunPhase(ApplyPhase.GameInit, TryPatchCategory);", "ReportPatchFailures(new TextObject(\"{=taom_patch_apply_phase_game_init}game initialization\"));");
        AssertOnceBetween(code, "TryPatchCategory(\"Patch_MissionTime_SetMovementOrder\");",
            "FeatureModuleHooks.RunPhase(ApplyPhase.FirstMission, TryPatchCategory);", "ReportPatchFailures(new TextObject(\"{=taom_patch_apply_phase_mission_start}mission start\"));");
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

    // An empty override compiles to "nop; ret" (2 bytes, Debug) or "ret" (1 byte, Release).
    // CampaignBehaviorBase.SyncData is abstract, so every concrete behavior has one.
    private static bool PersistsData(Type behaviorType)
    {
        var syncData = behaviorType.GetMethod("SyncData",
            BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(IDataStore) }, null);
        Assert.IsNotNull(syncData, $"{behaviorType.Name} has no SyncData(IDataStore).");
        var il = syncData!.GetMethodBody()?.GetILAsByteArray();
        return il != null && il.Length > 2;
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
