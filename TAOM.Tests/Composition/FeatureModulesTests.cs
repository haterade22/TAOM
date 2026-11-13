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
        // shadows the first. A Custom Battle slot that SubModule.RegisterCustomBattleModels fills by hand is
        // checked below; a campaign slot filled by hand with a different type is not checked yet.
        AssertDeclaredOnce(declared.Select(d => d.Decl.Target + ":" + d.Decl.SlotType.FullName), "model slot");
        AssertNotHandWiredToo(declared.Select(d => (d.Module, Type: d.Decl.ModelType)).ToList(), "registered");
    }

    // A type fills a slot when its base list names the slot's model or the slot's Default model. A file that only reads
    // Campaign.Current.Models.XxxModel fills nothing and must not count.
    private static bool FillsTheSlot(string strippedSource, string slotName) =>
        Regex.IsMatch(strippedSource, @"\bclass\s+\w+\s*(?:<[^>]*>)?\s*:\s*[^{;]*\b(?:Default)?" + Regex.Escape(slotName) + @"\b");

    private static string SlotName(Type slotType) =>
        slotType.Name.StartsWith("Default", StringComparison.Ordinal) ? slotType.Name.Substring("Default".Length) : slotType.Name;

    // A source scan, not reflection: GetTypes() on TAOM's assembly needs the game. Two models on one slot would make the later one
    // win and drop the other's rule (gamemodels.md rule 7). It replaces the per-feature copies of this scan.
    [TestMethod]
    public void EveryDeclaredGameModel_IsTheOnlyTaomFileFillingItsSlot()
    {
        var sources = RepoPaths.CSharpFiles()
            .Select(f => (Name: Path.GetFileName(f), Text: RepoPaths.StripComments(RepoPaths.Text(f))))
            .ToList();

        var wrong = new List<string>();
        foreach (var decl in FeatureModules.All.SelectMany(m => m.GameModels))
        {
            var slot = SlotName(decl.SlotType);
            var fillers = sources
                .Where(s => s.Text.IndexOf(slot, StringComparison.Ordinal) >= 0 && FillsTheSlot(s.Text, slot))
                .Select(s => s.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
            if (!fillers.SequenceEqual(new[] { decl.ModelType.Name + ".cs" }))
                wrong.Add($"{slot}: expected only {decl.ModelType.Name}.cs, found [{string.Join(", ", fillers)}]");
        }

        Assert.AreEqual(0, wrong.Count, string.Join("; ", wrong));
    }

    [DataTestMethod]
    [DataRow("public class TaomX : DefaultDefectionModel { }", true)]
    [DataRow("public class TaomX : TaleWorlds.CampaignSystem.ComponentInterfaces.DefectionModel { }", true)]
    [DataRow("public sealed class TaomX<T> : IFoo, DefectionModel\n{\n}", true)]
    [DataRow("public class Reader { void F() { var m = Campaign.Current.Models.DefectionModel; } }", false)]
    [DataRow("public class Reader : CampaignBehaviorBase\n{\n void F() { var m = Campaign.Current.Models.DefectionModel; }\n}", false)]
    [DataRow("public class Other : DefaultPartyWageModel { }", false)]
    public void FillsTheSlot_OnlyAClassDerivingFromTheModel_Matches(string source, bool expected)
    {
        Assert.AreEqual(expected, FillsTheSlot(source, "DefectionModel"));
    }

    [TestMethod]
    public void SlotName_StripsOneLeadingDefault()
    {
        Assert.AreEqual("DefectionModel", SlotName(typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultDefectionModel)));
        Assert.AreEqual("DefectionModel", SlotName(typeof(TaleWorlds.CampaignSystem.ComponentInterfaces.DefectionModel)));
    }

    // The module step (AddGameStartContent) runs after RegisterCustomBattleModels and MissionGameModels takes the LAST model
    // added for a slot, so a module declaration for a slot the Custom Battle step fills by hand would shadow it silently. The
    // Custom Battle damage twin (#788) is the case: it is hand-wired, so no module may declare AgentApplyDamageModel for it.
    [TestMethod]
    public void NoCustomBattleModelSlot_IsFilledByAModuleAndByRegisterCustomBattleModels()
    {
        var subModule = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        var start = subModule.IndexOf("private static void RegisterCustomBattleModels(", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "RegisterCustomBattleModels is gone; update this test.");
        var body = subModule.Substring(start, subModule.IndexOf("private static void", start + 20, StringComparison.Ordinal) - start);
        var handWiredSlots = Regex.Matches(body, @"\.AddModel<(\w+)>").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        Assert.IsTrue(handWiredSlots.Contains("AgentApplyDamageModel"), "the Custom Battle damage twin is added by hand");

        var shadowing = FeatureModules.All
            .SelectMany(m => m.GameModels.Select(d => (Module: m.Id, Decl: d)))
            .Where(x => x.Decl.Target == ModelTarget.CustomBattle && handWiredSlots.Contains(x.Decl.SlotType.Name))
            .Select(x => x.Module + ":" + x.Decl.SlotType.Name)
            .ToList();

        Assert.AreEqual(0, shadowing.Count,
            "a module declaration for a slot RegisterCustomBattleModels fills is added after it and shadows it: " + string.Join(", ", shadowing));
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
        var repoDir = RepoPaths.RepoPath();
        var readers = RepoPaths.CSharpFiles()
            .Select(f => f.Substring(repoDir.Length).TrimStart('\\', '/').Replace('\\', '/'))
            .Where(rel => RepoPaths.MentionsOutsideComments(Path.Combine(repoDir, rel), "IoC.Resolver"))
            .ToList();

        CollectionAssert.AreEquivalent(new[] { "Main/Composition/FeatureModuleHooks.cs" }, readers,
            "Only the feature-module hooks may read IoC.Resolver; a service takes its dependencies by constructor.");
    }

    [TestMethod]
    public void Kernel_IoCConfigure_AppliesTheClientSlotAfterEveryRegistration_BeforeThePostRegistrationEagerBlock()
    {
        var code = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true);

        AssertOnceBetween(code, "Modules = modules;", "ClientComposition.Apply(container);", "_container = container;");
        // The first call of the eager block; the next test pins that every such call follows Apply.
        AssertOnceBetween(code, "ClientComposition.Apply(container);",
            "FactionMapIoC.InitializePatchStatics(container);", "modules.InitializeStatics(container);");
    }

    // ADR-013: DryIoc keeps the instance it built, so anything resolved before the client composition
    // replaced core's null adapters keeps the null. Configure resolves nothing before Apply, and every
    // patch-static initialisation (which resolves) comes after it.
    [TestMethod]
    public void Kernel_IoCConfigure_ResolvesNothingBeforeTheClientSlot_AndEveryStaticsInitComesAfter()
    {
        var code = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true);
        var apply = code.IndexOf("ClientComposition.Apply(container);", StringComparison.Ordinal);
        var configure = code.IndexOf("public static void Configure()", StringComparison.Ordinal);
        Assert.IsTrue(configure >= 0 && apply > configure, "Configure or ClientComposition.Apply(container); was not found in IoC.cs");

        // The module runner's logger factory resolves lazily, when a module first fails: not before Apply.
        var beforeApply = code.Substring(configure, apply - configure).Replace("() => container.Resolve<IModLogger>()", "");
        Assert.IsFalse(beforeApply.Contains(".Resolve"),
            "Configure resolves a service before ClientComposition.Apply; the client's replacement adapters would miss it");

        var inits = Regex.Matches(code, @"Initialize\w*Statics\(container\)");
        Assert.IsTrue(inits.Count >= 5, "expected the eager patch-static block in IoC.cs, found " + inits.Count);
        foreach (Match init in inits)
            Assert.IsTrue(init.Index > apply, init.Value + " runs before ClientComposition.Apply");
        // Each call by name: the count above passes while one is deleted, and the camp overlay or the plate
        // settings then go missing with every test still green. All run after the client slot is filled.
        foreach (var call in new[]
        {
            "FactionMapIoC.InitializePatchStatics(container);",
            "FieldCampIoC.InitializePatchStatics(container);",
            "Features.Refuge.RefugeIoC.InitializePatchStatics(container);",
            "Features.UncapturableHeroes.UncapturableHeroesIoC.InitializePatchStatics(container);",
            "NameplateRelationIoC.InitializeWidgetStatics(container);",
        })
        {
            var at = code.IndexOf(call, StringComparison.Ordinal);
            Assert.IsTrue(at >= 0, call + " is missing from IoC.Configure");
            Assert.IsTrue(at > apply, call + " runs before ClientComposition.Apply");
        }
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
        foreach (var category in new[] { "Patch_MissionTime_SmartCavalryAI", "Patch_MissionTime_CompanionTactics" })
            AssertOnceBetween(code, "TryPatchCategory(\"" + category + "\");",
                "FeatureModuleHooks.RunPhase(ApplyPhase.FirstMission, TryPatchCategory);", "ReportPatchFailures(new TextObject(\"{=taom_patch_apply_phase_mission_start}mission start\"));");
        AssertOnceBetween(code, "new Features.MissionPerf.Hooks.MissionTickProfilerBehavior(",
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
